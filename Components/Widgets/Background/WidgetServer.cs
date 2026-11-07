using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Widgets.Background;



// the widgets' little web server on 127.0.0.1:<port> (plain sockets, like SpotifyLoginListener: windows only lets
// HttpListener use that address as admin):
//   /widget/<name>  the widget's page (OBS's browser source, or a browser to look at it)
//   /asset/<path>   a file inside Assets/ (the widget's images)
//   /ws/<name>      the page's live connection: the app sends it what to draw (Send), it sends nothing back
// Work happens off the UI thread; Joined is raised on whichever thread accepted the page, the owner posts it on
public sealed class WidgetServer : IDisposable
{
    // private



    private sealed class Client(WebSocket socket, string name)
    {
        public WebSocket Socket { get; } = socket;
        public string Name { get; } = name;
        public SemaphoreSlim SendLock { get; } = new(1, 1); // a websocket takes one send at a time
    }

    private readonly Func<string, string> _page; // the widget's name -> its page
    private readonly Func<string, (byte[] Data, string Type)?> _asset; // a path inside Assets/ -> the file
    private readonly List<Client> _clients;
    private TcpListener? _listener;
    private CancellationTokenSource? _run;



    // public



    public bool IsRunning => _listener != null;



    // events



    public event Action<string>? Joined; // a page for that widget connected (send it everything)



    // constructor



    public WidgetServer(Func<string, string> page, Func<string, (byte[] Data, string Type)?> asset)
    {
        _page = page;
        _asset = asset;
        _clients = [];
    }



    // public api



    // starts listening (stopping first if it was), the error says why it couldn't (e.g. the port is in use)
    public bool TryStart(int port, out string error)
    {
        Stop();
        error = "";

        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            error = $"port {port} is in use";
            return false;
        }

        _listener = listener;
        _run = new();
        _ = AcceptAsync(listener, _run.Token);
        return true;
    }



    public void Stop()
    {
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
        _listener?.Stop();
        _listener = null;

        List<Client> clients;
        lock (_clients)
        {
            clients = [.. _clients];
            _clients.Clear();
        }
        foreach (var client in clients) client.Socket.Abort();
    }



    // the names pages are open for right now
    public List<string> OpenNames()
    {
        lock (_clients) return _clients.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }



    // to every page of that widget (case ignored); a page that's gone is dropped
    public void Send(string name, string json)
    {
        List<Client> clients;
        lock (_clients) clients = _clients.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

        var bytes = Encoding.UTF8.GetBytes(json);
        foreach (var client in clients) _ = SendAsync(client, bytes);
    }



    // helpers



    private async Task AcceptAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient tcp;
            try
            {
                tcp = await listener.AcceptTcpClientAsync(ct);
            }
            catch (Exception)
            {
                return; // stopped
            }

            _ = Task.Run(() => ServeAsync(tcp, ct), ct);
        }
    }



    private async Task ServeAsync(TcpClient tcp, CancellationToken ct)
    {
        try
        {
            var stream = tcp.GetStream();
            var request = await ReadHeadAsync(stream, ct);
            if (request == null)
            {
                tcp.Dispose();
                return;
            }

            var target = request.Split("\r\n")[0].Split(' ') is { Length: >= 2 } parts ? parts[1] : "/";
            var path = Uri.UnescapeDataString(target.Split('?')[0]);

            if (path.StartsWith("/ws/") && request.Contains("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
            {
                await UpgradeAsync(stream, request, ct);
                await ListenAsync(WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true }), path["/ws/".Length..], ct);
                tcp.Dispose();
                return;
            }

            if (path.StartsWith("/widget/")) await RespondAsync(stream, "200 OK", "text/html; charset=utf-8", Encoding.UTF8.GetBytes(_page(path["/widget/".Length..])), ct);
            else if (path.StartsWith("/asset/") && _asset(path["/asset/".Length..]) is { } file) await RespondAsync(stream, "200 OK", file.Type, file.Data, ct);
            else await RespondAsync(stream, "404 Not Found", "text/plain", Encoding.UTF8.GetBytes("not found"), ct);
            tcp.Dispose();
        }
        catch (Exception)
        {
            tcp.Dispose(); // the page went away mid-request, or the server stopped
        }
    }



    // a page's connection: kept until it closes, anything it sends is ignored
    private async Task ListenAsync(WebSocket socket, string name, CancellationToken ct)
    {
        var client = new Client(socket, name);
        lock (_clients) _clients.Add(client);
        Joined?.Invoke(name);

        try
        {
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (Exception)
        {
            // closed or stopped
        }
        finally
        {
            lock (_clients) _clients.Remove(client);
            socket.Dispose();
        }
    }



    private async Task SendAsync(Client client, byte[] bytes)
    {
        await client.SendLock.WaitAsync();
        try
        {
            if (client.Socket.State == WebSocketState.Open)
                await client.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception)
        {
            lock (_clients) _clients.Remove(client);
        }
        finally
        {
            client.SendLock.Release();
        }
    }



    private static async Task<string?> ReadHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var received = new StringBuilder();
        while (!received.ToString().Contains("\r\n\r\n"))
        {
            if (received.Length > 65536) return null;
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) return null;
            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }
        return received.ToString();
    }



    // the websocket handshake: answer the page's key so it switches to websocket
    private static async Task UpgradeAsync(NetworkStream stream, string request, CancellationToken ct)
    {
        var key = request.Split("\r\n").First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), ct);
    }



    private static async Task RespondAsync(Stream stream, string status, string type, byte[] body, CancellationToken ct)
    {
        var head = $"HTTP/1.1 {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
        await stream.WriteAsync(body, ct);
    }



    // disposal



    public void Dispose() => Stop();
}
