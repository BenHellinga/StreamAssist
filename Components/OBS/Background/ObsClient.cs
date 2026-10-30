using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.OBS.Background;



// OBS refused a request or couldn't be reached, the message is OBS's reason (e.g. "No source was found by the name of x")
public class ObsRequestException(string message) : Exception(message);



// one connection to obs-websocket (protocol v5, built into OBS 28+): connects and identifies (answering OBS's password
// challenge if it has one), then sends requests and matches each answer to its request by id. ObsManager is the only
// user; a new connection is a new client. No OBS events are asked for yet (nothing triggers on them)
public sealed class ObsClient : IDisposable
{
    // private



    // obs-websocket opcodes
    private const int Hello = 0;
    private const int Identify = 1;
    private const int Identified = 2;
    private const int Request = 6;
    private const int RequestResponse = 7;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly ClientWebSocket _socket;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending;
    private readonly SemaphoreSlim _sendLock; // a websocket takes one message at a time
    private readonly CancellationTokenSource _stop;



    // events



    public event Action<string>? Closed; // the connection dropped (not when disposed), with why



    // constructor



    private ObsClient()
    {
        _socket = new();
        _pending = new();
        _sendLock = new(1, 1);
        _stop = new();
    }



    // public api



    // address is host:port (e.g. localhost:4455), throws ObsRequestException with a short reason if it can't
    public static async Task<ObsClient> ConnectAsync(string address, string password, CancellationToken ct)
    {
        var client = new ObsClient();
        try
        {
            await client.HandshakeAsync(address, password, ct);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _ = client.ReceiveLoopAsync();
        return client;
    }



    // sends one request (e.g. "GetSceneList") and returns its responseData (an empty object if it has none)
    // throws ObsRequestException with OBS's reason if OBS refused it, didn't answer, or the connection is gone
    public async Task<JsonElement> RequestAsync(string requestType, JsonObject? requestData, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString("N");
        var answer = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = answer;

        try
        {
            var data = new JsonObject { ["requestType"] = requestType, ["requestId"] = id };
            if (requestData != null) data["requestData"] = requestData;
            await SendAsync(Request, data, ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
            timeout.CancelAfter(RequestTimeout);
            using var registration = timeout.Token.Register(() => answer.TrySetException(new ObsRequestException(
                ct.IsCancellationRequested ? "cancelled" : _stop.IsCancellationRequested ? "obs disconnected" : "obs didn't answer")));

            var response = await answer.Task;
            var status = response.GetProperty("requestStatus");
            if (!status.GetProperty("result").GetBoolean())
            {
                var comment = status.TryGetProperty("comment", out var c) ? c.GetString() : null;
                throw new ObsRequestException(comment ?? $"obs said no (code {status.GetProperty("code").GetInt32()})");
            }

            return response.TryGetProperty("responseData", out var responseData) ? responseData : JsonDocument.Parse("{}").RootElement;
        }
        catch (WebSocketException)
        {
            throw new ObsRequestException("obs disconnected");
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }



    // the password proof obs-websocket asks for: base64(sha256(base64(sha256(password + salt)) + challenge))
    public static string Authentication(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
    }



    // helpers



    private async Task HandshakeAsync(string address, string password, CancellationToken ct)
    {
        try
        {
            await _socket.ConnectAsync(new Uri($"ws://{address}"), ct);
        }
        catch (Exception ex) when (ex is WebSocketException or UriFormatException)
        {
            throw new ObsRequestException(ex is UriFormatException ? $"'{address}' isn't host:port" : "nothing at that address, is obs open with its websocket server on");
        }

        var hello = await ReceiveAsync(ct);
        if (hello.GetProperty("op").GetInt32() != Hello) throw new ObsRequestException("didn't say hello like obs does");

        var identify = new JsonObject { ["rpcVersion"] = 1, ["eventSubscriptions"] = 0 };
        if (hello.GetProperty("d").TryGetProperty("authentication", out var auth))
        {
            if (password == "") throw new ObsRequestException("obs needs its websocket password");
            identify["authentication"] = Authentication(password, auth.GetProperty("salt").GetString()!, auth.GetProperty("challenge").GetString()!);
        }

        await SendAsync(Identify, identify, ct);

        // a wrong password closes the connection (OBS says "Authentication failed.")
        var identified = await ReceiveAsync(ct);
        if (identified.GetProperty("op").GetInt32() != Identified) throw new ObsRequestException("obs didn't accept the connection");
    }



    // reads answers until the connection drops or this client is disposed, then fails whatever was still waiting
    private async Task ReceiveLoopAsync()
    {
        string? reason = null;
        try
        {
            while (true)
            {
                var message = await ReceiveAsync(_stop.Token);
                if (message.GetProperty("op").GetInt32() != RequestResponse) continue;

                var data = message.GetProperty("d");
                if (_pending.TryGetValue(data.GetProperty("requestId").GetString()!, out var answer)) answer.TrySetResult(data);
            }
        }
        catch (Exception ex)
        {
            if (!_stop.IsCancellationRequested) reason = ex.Message;
        }

        foreach (var answer in _pending.Values) answer.TrySetException(new ObsRequestException("obs disconnected"));
        if (reason != null) Closed?.Invoke(reason);
    }



    private async Task SendAsync(int op, JsonObject data, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(new JsonObject { ["op"] = op, ["d"] = data }.ToJsonString());

        await _sendLock.WaitAsync(ct);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }



    // one whole message (it can arrive in several pieces), a close from OBS throws with OBS's reason
    private async Task<JsonElement> ReceiveAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await _socket.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                var why = _socket.CloseStatusDescription;
                throw new ObsRequestException(string.IsNullOrWhiteSpace(why) ? "obs closed the connection" : why.TrimEnd('.').ToLowerInvariant());
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
        }
    }



    // disposal



    public void Dispose()
    {
        _stop.Cancel();

        // tell OBS goodbye without waiting for it, then let the socket go
        if (_socket.State != WebSocketState.Open)
        {
            _socket.Dispose();
            return;
        }

        _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None)
            .ContinueWith(_ => _socket.Dispose(), TaskScheduler.Default);
    }
}
