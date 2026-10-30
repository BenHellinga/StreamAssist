using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Spotify.Background;



// waits for the browser to come back from spotify's login page to http://127.0.0.1:<port>/callback and reads the code
// from it. A plain socket rather than HttpListener, since windows only lets HttpListener use 127.0.0.1 as admin
public static class SpotifyLoginListener
{
    // the redirect's query (code and state, or error), once the browser opens /callback
    public static async Task<Dictionary<string, string>> WaitForCallbackAsync(int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            throw new Exception($"port {port} is in use");
        }

        try
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                using var stream = client.GetStream();

                var target = await ReadRequestTargetAsync(stream, ct);
                var isCallback = target != null && target.StartsWith("/callback");

                var page = isCallback
                    ? "<html><body style=\"font-family:sans-serif\">StreamAssist is logged in to Spotify, you can close this tab.</body></html>"
                    : "";
                var bytes = Encoding.UTF8.GetBytes(page);
                var head = $"HTTP/1.1 {(isCallback ? "200 OK" : "404 Not Found")}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct);
                await stream.WriteAsync(bytes, ct);

                // anything else the browser asks for (e.g. /favicon.ico) is ignored
                if (isCallback) return ReadQuery(target!);
            }
        }
        finally
        {
            listener.Stop();
        }
    }



    // the path and query of "GET /callback?code=... HTTP/1.1"
    private static async Task<string?> ReadRequestTargetAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var received = new StringBuilder();
        while (!received.ToString().Contains("\r\n\r\n") && received.Length < 65536)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            received.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        var firstLine = received.ToString().Split("\r\n")[0].Split(' ');
        return firstLine.Length >= 2 ? firstLine[1] : null;
    }



    private static Dictionary<string, string> ReadQuery(string target)
    {
        var values = new Dictionary<string, string>();
        var question = target.IndexOf('?');
        if (question < 0) return values;

        foreach (var pair in target[(question + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = Uri.UnescapeDataString(equals < 0 ? pair : pair[..equals]);
            values[key] = equals < 0 ? "" : Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
        }
        return values;
    }
}
