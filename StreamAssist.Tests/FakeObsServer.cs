using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StreamAssist.Tests;



// a pretend OBS for the tests: speaks enough obs-websocket v5 on a free local port (hello with a password challenge,
// identify, and the requests ObsManager uses), with two scenes, a few sources and inputs whose state requests change
public sealed class FakeObsServer : IDisposable
{
    public const string Password = "secret";
    private const string Salt = "salty";
    private const string Challenge = "challenge";

    public sealed class Item(int id, string source)
    {
        public int Id = id;
        public string Source = source;
        public bool Enabled = true;
        public JsonObject Transform = new()
        {
            ["positionX"] = 10.0, ["positionY"] = 20.0, ["rotation"] = 0.0, ["scaleX"] = 1.0, ["scaleY"] = 1.0,
            ["sourceWidth"] = 400.0, ["sourceHeight"] = 100.0, ["width"] = 400.0, ["height"] = 100.0,
            ["cropLeft"] = 0, ["cropRight"] = 0, ["cropTop"] = 0, ["cropBottom"] = 0,
        };
    }

    public sealed class Input(string kind)
    {
        public string Kind = kind;
        public double VolumeDb = -5;
        public bool Muted;
        public JsonObject Settings = new();
    }

    private readonly TcpListener _listener;
    private WebSocket? _socket;

    public string CurrentScene = "Gaming";
    public readonly List<string> SceneOrder = ["Gaming", "Just Chatting"]; // top to bottom, like OBS's Scenes list
    public readonly Dictionary<string, List<Item>> Scenes = new()
    {
        ["Gaming"] = [new(1, "Cam"), new(2, "Title")], // top to bottom, like OBS's Sources list
        ["Just Chatting"] = [new(1, "Cam")],
    };
    public readonly Dictionary<string, Input> Inputs = new()
    {
        ["Mic"] = new("wasapi_input_capture"),
        ["Title"] = new("text_gdiplus_v3") { Settings = new() { ["text"] = "hello" } },
        ["Cam"] = new("dshow_input"),
    };

    public string Address => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";



    public FakeObsServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }



    // closes the connection like OBS shutting down
    public void Drop() => _socket?.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "OBS is shutting down.", CancellationToken.None).Wait(1000);



    private async Task AcceptLoopAsync()
    {
        while (true)
        {
            TcpClient tcp;
            try { tcp = await _listener.AcceptTcpClientAsync(); }
            catch { return; }
            _ = Task.Run(() => ServeAsync(tcp));
        }
    }



    private async Task ServeAsync(TcpClient tcp)
    {
        try
        {
            var stream = tcp.GetStream();
            await UpgradeAsync(stream);
            var socket = WebSocket.CreateFromStream(stream, new WebSocketCreationOptions { IsServer = true });
            _socket = socket;

            await SendAsync(socket, 0, new JsonObject { ["obsWebSocketVersion"] = "5.0.0", ["rpcVersion"] = 1, ["authentication"] = new JsonObject { ["challenge"] = Challenge, ["salt"] = Salt } });

            var identify = await ReceiveAsync(socket);
            var expected = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(
                Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Password + Salt))) + Challenge)));
            if (identify?["d"]?["authentication"]?.GetValue<string>() != expected)
            {
                await socket.CloseOutputAsync((WebSocketCloseStatus)4009, "Authentication failed.", CancellationToken.None);
                return;
            }

            await SendAsync(socket, 2, new JsonObject { ["negotiatedRpcVersion"] = 1 });

            while (socket.State == WebSocketState.Open)
            {
                var message = await ReceiveAsync(socket);
                if (message == null) return;
                var d = message["d"]!;
                var type = d["requestType"]!.GetValue<string>();
                JsonObject response;
                try
                {
                    response = new JsonObject
                    {
                        ["requestType"] = type,
                        ["requestId"] = d["requestId"]!.GetValue<string>(),
                        ["requestStatus"] = new JsonObject { ["result"] = true, ["code"] = 100 },
                        ["responseData"] = Handle(type, d["requestData"] as JsonObject ?? new JsonObject()),
                    };
                }
                catch (InvalidOperationException ex)
                {
                    response = new JsonObject
                    {
                        ["requestType"] = type,
                        ["requestId"] = d["requestId"]!.GetValue<string>(),
                        ["requestStatus"] = new JsonObject { ["result"] = false, ["code"] = 600, ["comment"] = ex.Message },
                    };
                }
                await SendAsync(socket, 7, response);
            }
        }
        catch
        {
            // the test is over or the client went away
        }
    }



    private JsonObject Handle(string type, JsonObject data)
    {
        string Str(string key) => data[key]!.GetValue<string>();
        Item ItemFor() => Scene(Str("sceneName")).FirstOrDefault(i => i.Id == data["sceneItemId"]!.GetValue<int>()) ?? throw Missing("scene item");
        Input InputFor() => Inputs.TryGetValue(Str("inputName"), out var input) ? input : throw Missing(Str("inputName"));

        switch (type)
        {
            case "GetCurrentProgramScene": return new() { ["currentProgramSceneName"] = CurrentScene };
            case "SetCurrentProgramScene": Scene(Str("sceneName")); CurrentScene = Str("sceneName"); return new();
            // OBS lists scenes and scene items bottom to top
            case "GetSceneList": return new() { ["scenes"] = new JsonArray(SceneOrder.AsEnumerable().Reverse().Select(s => (JsonNode)new JsonObject { ["sceneName"] = s }).ToArray()) };
            case "GetSceneItemList": return new() { ["sceneItems"] = new JsonArray(Scene(Str("sceneName")).AsEnumerable().Reverse().Select(i => (JsonNode)new JsonObject { ["sourceName"] = i.Source, ["sceneItemId"] = i.Id }).ToArray()) };
            case "GetSceneItemId": return new() { ["sceneItemId"] = (Scene(Str("sceneName")).FirstOrDefault(i => i.Source == Str("sourceName")) ?? throw Missing(Str("sourceName"))).Id };
            case "GetSceneItemEnabled": return new() { ["sceneItemEnabled"] = ItemFor().Enabled };
            case "SetSceneItemEnabled": ItemFor().Enabled = data["sceneItemEnabled"]!.GetValue<bool>(); return new();
            case "GetSceneItemTransform": return new() { ["sceneItemTransform"] = ItemFor().Transform.DeepClone() };
            case "SetSceneItemTransform":
                var item = ItemFor();
                foreach (var (key, value) in data["sceneItemTransform"]!.AsObject()) item.Transform[key] = value!.DeepClone();
                return new();
            case "GetInputVolume": return new() { ["inputVolumeDb"] = InputFor().VolumeDb };
            case "SetInputVolume": InputFor().VolumeDb = data["inputVolumeDb"]!.GetValue<double>(); return new();
            case "GetInputMute": return new() { ["inputMuted"] = InputFor().Muted };
            case "SetInputMute": InputFor().Muted = data["inputMuted"]!.GetValue<bool>(); return new();
            case "ToggleInputMute": var toggled = InputFor(); toggled.Muted = !toggled.Muted; return new() { ["inputMuted"] = toggled.Muted };
            case "GetInputSettings": var input = InputFor(); return new() { ["inputSettings"] = input.Settings.DeepClone(), ["inputKind"] = input.Kind };
            case "GetInputDefaultSettings":
                return new() { ["defaultInputSettings"] = Str("inputKind") == "text_gdiplus_v3" ? new JsonObject { ["text"] = "", ["opacity"] = 100, ["vertical"] = false } : new JsonObject() };
            case "SetInputSettings":
                var target = InputFor();
                foreach (var (key, value) in data["inputSettings"]!.AsObject()) target.Settings[key] = value!.DeepClone();
                return new();
            default: throw new InvalidOperationException($"Unknown request type {type}.");
        }
    }



    private List<Item> Scene(string name) => Scenes.TryGetValue(name, out var items) ? items : throw Missing(name);



    private static InvalidOperationException Missing(string name) => new($"No source was found by the name of `{name}`.");



    // the websocket handshake: answer the client's key so it switches to websocket
    private static async Task UpgradeAsync(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var request = "";
        while (!request.Contains("\r\n\r\n"))
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0) throw new IOException("closed");
            request += Encoding.ASCII.GetString(buffer, 0, read);
        }

        var key = request.Split("\r\n").First(l => l.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)).Split(':', 2)[1].Trim();
        var accept = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
        var response = $"HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {accept}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
    }



    private static async Task SendAsync(WebSocket socket, int op, JsonObject d) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(new JsonObject { ["op"] = op, ["d"] = d }.ToJsonString()), WebSocketMessageType.Text, true, CancellationToken.None);



    private static async Task<JsonNode?> ReceiveAsync(WebSocket socket)
    {
        var buffer = new byte[65536];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) return JsonNode.Parse(stream.ToArray());
        }
    }



    public void Dispose()
    {
        try { _socket?.Abort(); } catch { }
        _listener.Stop();
    }
}
