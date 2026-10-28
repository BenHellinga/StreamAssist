using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StreamAssist.Components.Chatbot.Background;



// one message from the eventsub websocket, only the fields we use
public sealed record EventSubMessage(
    string MessageType,
    string? SessionId = null,
    int KeepaliveSeconds = 0,
    string? ReconnectUrl = null,
    TwitchEvent? Event = null,
    string? RevokedType = null,
    string? RevokedReason = null)
{
    // message format: https://dev.twitch.tv/docs/eventsub/websocket-reference
    public static EventSubMessage Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var metadata = doc.RootElement.GetProperty("metadata");
        var payload = doc.RootElement.GetProperty("payload");
        var type = metadata.GetProperty("message_type").GetString() ?? "";

        switch (type)
        {
            case "session_welcome":
            {
                var session = payload.GetProperty("session");
                return new(type,
                    SessionId: session.GetProperty("id").GetString(),
                    KeepaliveSeconds: session.TryGetProperty("keepalive_timeout_seconds", out var keepalive) && keepalive.ValueKind == JsonValueKind.Number ? keepalive.GetInt32() : 10);
            }

            case "session_reconnect":
                return new(type, ReconnectUrl: payload.GetProperty("session").GetProperty("reconnect_url").GetString());

            case "notification":
                return new(type, Event: new TwitchEvent(
                    metadata.GetProperty("subscription_type").GetString() ?? "",
                    payload.GetProperty("event").Clone(),
                    metadata.TryGetProperty("message_timestamp", out var sent) && DateTimeOffset.TryParse(sent.GetString(), out var time) ? time : DateTimeOffset.Now));

            case "revocation":
            {
                var subscription = payload.GetProperty("subscription");
                return new(type,
                    RevokedType: subscription.GetProperty("type").GetString(),
                    RevokedReason: subscription.GetProperty("status").GetString());
            }

            default:
                return new(type); // session_keepalive, or something new
        }
    }
}



// keeps a connection to twitch's eventsub websocket and hands out what arrives
// (all events are raised on the background thread that reads the socket)
public sealed class TwitchEventSubClient
{
    // private



    private const string DefaultUrl = "wss://eventsub.wss.twitch.tv/ws";



    // events



    public event Action<string>? SessionStarted; // session id, subscriptions must be created within 10 seconds
    public event Action<TwitchEvent>? EventReceived;
    public event Action<string, string>? SubscriptionRevoked; // type, reason



    // public api



    // runs until cancelled or the connection drops (then it throws, the caller decides whether to retry)
    public async Task RunAsync(CancellationToken ct)
    {
        var url = DefaultUrl;
        var isReconnect = false;

        while (true)
        {
            using var socket = new ClientWebSocket();
            await socket.ConnectAsync(new Uri(url), ct);

            // twitch asked us to move to a new server, the subscriptions move with us
            var reconnectUrl = await ReadUntilReconnectAsync(socket, isReconnect, ct);
            url = reconnectUrl;
            isReconnect = true;

            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "reconnecting", CancellationToken.None); }
            catch (WebSocketException) { }
        }
    }



    // helpers



    // reads messages until twitch says to reconnect (returns the new url), throws if the connection dies
    private async Task<string> ReadUntilReconnectAsync(ClientWebSocket socket, bool isReconnect, CancellationToken ct)
    {
        var keepalive = TimeSpan.FromSeconds(30); // replaced by the welcome message's value

        while (true)
        {
            // twitch sends something (at least a keepalive) every keepalive period, silence means the connection is dead
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(keepalive + TimeSpan.FromSeconds(10));

            string json;
            try
            {
                json = await ReceiveTextAsync(socket, timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new Exception("Twitch connection went quiet");
            }

            var message = EventSubMessage.Parse(json);

            switch (message.MessageType)
            {
                case "session_welcome":
                    keepalive = TimeSpan.FromSeconds(message.KeepaliveSeconds);
                    if (!isReconnect) SessionStarted?.Invoke(message.SessionId!);
                    break;

                case "notification":
                    EventReceived?.Invoke(message.Event!);
                    break;

                case "session_reconnect":
                    return message.ReconnectUrl!;

                case "revocation":
                    SubscriptionRevoked?.Invoke(message.RevokedType ?? "?", message.RevokedReason ?? "?");
                    break;
            }
        }
    }



    // a websocket message can arrive in several pieces, put them back together
    private static async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);

            if (result.MessageType == WebSocketMessageType.Close)
                throw new Exception($"Twitch closed the connection ({socket.CloseStatusDescription ?? result.CloseStatus?.ToString()})");

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
