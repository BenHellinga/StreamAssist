using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.OBS.Background;



// what a show/hide or mute block asks for
public enum ObsToggle { On, Off, Toggle }



// keeps the OBS connection the model asks for (one attempt, like every connection: a failure or a drop is logged once
// and left as Failed for Retry), and does what the OBS blocks ask. Each request logs its own failure and returns
// false, so the block that asked doesn't have to. A scene left empty means the scene that's live right now
public class ObsManager : IDisposable
{
    // private



    private readonly ObsComponentModel _model;
    private CancellationTokenSource? _run;
    private ObsClient? _client;



    // public



    public ConnectionStatus Status { get; private set; }
    public string StatusMessage { get; private set; } // short, e.g. "Connected to localhost:4455"
    public bool IsConnected => _client != null;



    // events



    public event Action? StatusChanged; // always on the UI thread



    // constructor



    public ObsManager(ObsComponentModel model)
    {
        _model = model;
        _run = null;
        _client = null;

        Status = ConnectionStatus.NotConnected;
        StatusMessage = "Not connected";

        _model.ConnectionChanged += OnConnectionChanged;

        if (_model.IsEnabled) Start();
    }



    // scenes



    public Task<(bool Ok, string? Value)> TryGetCurrentSceneAsync() =>
        TryRequestAsync("read obs scene", CurrentSceneAsync);



    public Task<(bool Ok, bool Value)> TrySetCurrentSceneAsync(string scene) =>
        TryRequestAsync($"switch to scene '{scene}'", async client =>
        {
            await client.RequestAsync("SetCurrentProgramScene", new JsonObject { ["sceneName"] = scene });
            return true;
        });



    // every scene's name, top to bottom as OBS lists them
    public Task<(bool Ok, List<string>? Value)> TryGetScenesAsync() =>
        TryRequestAsync("read obs scenes", async client =>
        {
            var data = await client.RequestAsync("GetSceneList", null);
            // OBS lists them bottom to top
            return data.GetProperty("scenes").EnumerateArray().Reverse().Select(s => s.GetProperty("sceneName").GetString()!).ToList();
        });



    // every source in a scene, top to bottom as the Sources list shows them
    public Task<(bool Ok, List<string>? Value)> TryGetSourcesAsync(string scene) =>
        TryRequestAsync("read obs sources", async client =>
        {
            var data = await client.RequestAsync("GetSceneItemList", new JsonObject { ["sceneName"] = await SceneOrCurrentAsync(client, scene) });
            return data.GetProperty("sceneItems").EnumerateArray().Reverse().Select(i => i.GetProperty("sourceName").GetString()!).ToList();
        });



    // sources in a scene



    public Task<(bool Ok, bool Value)> TryGetSourceVisibleAsync(string scene, string source) =>
        TryRequestAsync($"read if '{source}' is visible", async client =>
        {
            var (sceneName, id) = await SceneItemAsync(client, scene, source);
            var data = await client.RequestAsync("GetSceneItemEnabled", new JsonObject { ["sceneName"] = sceneName, ["sceneItemId"] = id });
            return data.GetProperty("sceneItemEnabled").GetBoolean();
        });



    public Task<(bool Ok, bool Value)> TrySetSourceVisibleAsync(string scene, string source, ObsToggle change) =>
        TryRequestAsync($"show/hide '{source}'", async client =>
        {
            var (sceneName, id) = await SceneItemAsync(client, scene, source);
            var visible = change == ObsToggle.On;
            if (change == ObsToggle.Toggle)
            {
                var data = await client.RequestAsync("GetSceneItemEnabled", new JsonObject { ["sceneName"] = sceneName, ["sceneItemId"] = id });
                visible = !data.GetProperty("sceneItemEnabled").GetBoolean();
            }

            await client.RequestAsync("SetSceneItemEnabled", new JsonObject { ["sceneName"] = sceneName, ["sceneItemId"] = id, ["sceneItemEnabled"] = visible });
            return true;
        });



    public Task<(bool Ok, double Value)> TryGetTransformAsync(string scene, string source, ObsTransform transform) =>
        TryRequestAsync($"read {transform.Name.ToLowerInvariant()} of '{source}'", async client =>
        {
            var current = await TransformAsync(client, scene, source);
            return current.Transform.GetProperty(transform.Key).GetDouble();
        });



    // width / height set the scale (OBS works them out from the source's own size times the scale)
    // the 9 points a resize can keep still, as fractions across and down the source (0 = left / top, 1 = right / bottom)
    public static readonly IReadOnlyList<(string Name, double X, double Y)> Anchors =
    [
        ("Top Left", 0, 0), ("Top", 0.5, 0), ("Top Right", 1, 0),
        ("Left", 0, 0.5), ("Center", 0.5, 0.5), ("Right", 1, 0.5),
        ("Bottom Left", 0, 1), ("Bottom", 0.5, 1), ("Bottom Right", 1, 1),
    ];



    // resizes a source in a scene to the size newSize gives from its current one (on-screen pixels), keeping the anchor
    // point (e.g. its bottom-right corner) where it is. Its position is the point its alignment says (OBS: 1 left,
    // 2 right, 4 top, 8 bottom, else centre), so the top-left is worked out from that and the new position put back the
    // same way. Rotation isn't taken into account
    public Task<(bool Ok, bool Value)> TryResizeAsync(string scene, string source, Func<double, double, (double Width, double Height)> newSize, string anchor) =>
        TryRequestAsync($"resize '{source}'", async client =>
        {
            var (_, fx, fy) = Anchors.FirstOrDefault(a => a.Name == anchor);
            if (Anchors.All(a => a.Name != anchor)) throw new ObsRequestException($"no anchor '{anchor}'");

            var current = await TransformAsync(client, scene, source);
            var t = current.Transform;
            double width = t.GetProperty("width").GetDouble(), height = t.GetProperty("height").GetDouble();
            if (width == 0 || height == 0) throw new ObsRequestException("it has no size to scale");
            var alignment = t.GetProperty("alignment").GetInt32();
            double Offset(double size, int start, int end) => (alignment & start) != 0 ? 0 : (alignment & end) != 0 ? size : size / 2;

            var left = t.GetProperty("positionX").GetDouble() - Offset(width, 1, 2);
            var top = t.GetProperty("positionY").GetDouble() - Offset(height, 4, 8);
            var (anchorX, anchorY) = (left + fx * width, top + fy * height);

            var (newWidth, newHeight) = newSize(width, height);
            if (newWidth <= 0 || newHeight <= 0) throw new ObsRequestException("the new size has to be above 0");
            var (newLeft, newTop) = (anchorX - fx * newWidth, anchorY - fy * newHeight);

            await client.RequestAsync("SetSceneItemTransform", new JsonObject
            {
                ["sceneName"] = current.Scene,
                ["sceneItemId"] = current.Id,
                ["sceneItemTransform"] = new JsonObject
                {
                    ["scaleX"] = t.GetProperty("scaleX").GetDouble() * newWidth / width,
                    ["scaleY"] = t.GetProperty("scaleY").GetDouble() * newHeight / height,
                    ["positionX"] = newLeft + Offset(newWidth, 1, 2),
                    ["positionY"] = newTop + Offset(newHeight, 4, 8),
                },
            });
            return true;
        });



    public Task<(bool Ok, bool Value)> TrySetTransformAsync(string scene, string source, ObsTransform transform, double value) =>
        TryRequestAsync($"set {transform.Name.ToLowerInvariant()} of '{source}'", async client =>
        {
            var current = await TransformAsync(client, scene, source);
            var changes = new JsonObject();

            if (transform.Key is "width" or "height")
            {
                var size = current.Transform.GetProperty(transform.Key == "width" ? "sourceWidth" : "sourceHeight").GetDouble();
                if (size == 0) throw new ObsRequestException("it has no size to scale");
                changes[transform.Key == "width" ? "scaleX" : "scaleY"] = value / size;
            }
            else
            {
                changes[transform.Key] = value;
            }

            await client.RequestAsync("SetSceneItemTransform", new JsonObject
            {
                ["sceneName"] = current.Scene,
                ["sceneItemId"] = current.Id,
                ["sceneItemTransform"] = changes,
            });
            return true;
        });



    // audio



    public Task<(bool Ok, double Value)> TryGetVolumeAsync(string source) =>
        TryRequestAsync($"read volume of '{source}'", async client =>
        {
            var data = await client.RequestAsync("GetInputVolume", new JsonObject { ["inputName"] = source });
            return data.GetProperty("inputVolumeDb").GetDouble();
        });



    // in dB like OBS's mixer: 0 is full volume, lower is quieter (OBS takes -100 to 26)
    public Task<(bool Ok, bool Value)> TrySetVolumeAsync(string source, double db) =>
        TryRequestAsync($"set volume of '{source}'", async client =>
        {
            await client.RequestAsync("SetInputVolume", new JsonObject { ["inputName"] = source, ["inputVolumeDb"] = Math.Clamp(db, -100, 26) });
            return true;
        });



    public Task<(bool Ok, bool Value)> TryGetMutedAsync(string source) =>
        TryRequestAsync($"read if '{source}' is muted", async client =>
        {
            var data = await client.RequestAsync("GetInputMute", new JsonObject { ["inputName"] = source });
            return data.GetProperty("inputMuted").GetBoolean();
        });



    public Task<(bool Ok, bool Value)> TrySetMutedAsync(string source, ObsToggle change) =>
        TryRequestAsync($"mute/unmute '{source}'", async client =>
        {
            if (change == ObsToggle.Toggle) await client.RequestAsync("ToggleInputMute", new JsonObject { ["inputName"] = source });
            else await client.RequestAsync("SetInputMute", new JsonObject { ["inputName"] = source, ["inputMuted"] = change == ObsToggle.On });
            return true;
        });



    // a filter on a source (or a scene) on, off or toggled
    public Task<(bool Ok, bool Value)> TrySetFilterEnabledAsync(string source, string filter, ObsToggle change) =>
        TryRequestAsync($"turn filter '{filter}' on '{source}' on/off", async client =>
        {
            var enabled = change == ObsToggle.On;
            if (change == ObsToggle.Toggle)
            {
                var now = await client.RequestAsync("GetSourceFilter", new JsonObject { ["sourceName"] = source, ["filterName"] = filter });
                enabled = !now.GetProperty("filterEnabled").GetBoolean();
            }

            await client.RequestAsync("SetSourceFilterEnabled", new JsonObject { ["sourceName"] = source, ["filterName"] = filter, ["filterEnabled"] = enabled });
            return true;
        });



    // OBS's outputs by name (Recording, Streaming, Replay Buffer, Virtual Camera) started, stopped or toggled; starting one
    // that's already running (or stopping a stopped one) sends nothing, it's already what was asked
    public static readonly IReadOnlyList<string> Outputs = ["Recording", "Streaming", "Replay Buffer", "Virtual Camera"];

    public Task<(bool Ok, bool Value)> TrySetOutputAsync(string output, ObsToggle change) =>
        TryRequestAsync($"start/stop {output.ToLowerInvariant()}", async client =>
        {
            var name = output switch
            {
                "Recording" => "Record",
                "Streaming" => "Stream",
                "Replay Buffer" => "ReplayBuffer",
                "Virtual Camera" => "VirtualCam",
                _ => throw new Exception($"no output '{output}'"),
            };

            if (change == ObsToggle.Toggle)
            {
                await client.RequestAsync($"Toggle{name}", null);
                return true;
            }

            var active = (await client.RequestAsync($"Get{name}Status", null)).GetProperty("outputActive").GetBoolean();
            if (active != (change == ObsToggle.On)) await client.RequestAsync((change == ObsToggle.On ? "Start" : "Stop") + name, null);
            return true;
        });



    // saves the replay buffer's last moments to a file (the replay buffer has to be running)
    public Task<(bool Ok, bool Value)> TrySaveReplayAsync() =>
        TryRequestAsync("save replay", async client =>
        {
            await client.RequestAsync("SaveReplayBuffer", null);
            return true;
        });



    // source settings (what a source's Properties window shows, e.g. a text source's "text")



    // the setting's value, or its default if it was never changed (OBS only stores changed ones)
    public Task<(bool Ok, JsonElement Value)> TryGetSourceSettingAsync(string source, string key) =>
        TryRequestAsync($"read setting '{key}' of '{source}'", async client =>
            await SourceSettingAsync(client, source, key) ?? throw new ObsRequestException($"it has no setting '{key}'"));



    // value is text, a number or true/false; it's stored as the kind the setting already is (e.g. typed "5" stays a
    // number for a number setting), a setting OBS doesn't know yet is stored as it reads
    public Task<(bool Ok, bool Value)> TrySetSourceSettingAsync(string source, string key, object value) =>
        TryRequestAsync($"set setting '{key}' of '{source}'", async client =>
        {
            var existing = await SourceSettingAsync(client, source, key);
            await client.RequestAsync("SetInputSettings", new JsonObject
            {
                ["inputName"] = source,
                ["inputSettings"] = new JsonObject { [key] = ToJson(value, existing?.ValueKind) },
                ["overlay"] = true,
            });
            return true;
        });



    // model callbacks



    // the address, password or enabled changed (all at once, so one restart), or a reconnect was asked for
    private void OnConnectionChanged()
    {
        Stop();
        if (_model.IsEnabled) Start();
    }



    // connection



    private void Start()
    {
        _run = new CancellationTokenSource();
        _ = ConnectAsync(_model.Address, _model.Password, _run.Token);
    }



    private void Stop()
    {
        _run?.Cancel();
        _run?.Dispose();
        _run = null;

        _client?.Dispose();
        _client = null;
        SetStatus(ConnectionStatus.NotConnected, "Not connected");
    }



    private async Task ConnectAsync(string address, string password, CancellationToken ct)
    {
        SetStatus(ConnectionStatus.Connecting, "Connecting...");

        if (!CredentialStore.IsReadable)
        {
            SetStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError("can't connect to obs, auth.json can't be read");
            return;
        }

        ObsClient client;
        try
        {
            client = await ObsClient.ConnectAsync(address, password, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;

            SetStatus(ConnectionStatus.Failed, "Not connected");
            AppLog.WriteError($"can't connect to obs ({ex.Message})");
            return;
        }

        if (ct.IsCancellationRequested)
        {
            client.Dispose();
            return;
        }

        _client = client;
        client.Closed += reason => OnClosed(client, reason);
        SetStatus(ConnectionStatus.Connected, $"Connected to {address}");
    }



    // obs went away (closed, crashed, websocket server turned off): say so once, Retry connects again
    private void OnClosed(ObsClient client, string reason) => Dispatcher.UIThread.Post(() =>
    {
        if (_client != client) return; // an older connection, already replaced or stopped

        _client = null;
        client.Dispose();
        SetStatus(ConnectionStatus.Failed, "Disconnected");
        AppLog.WriteWarning($"obs disconnected ({reason})");
    });



    // requests



    // runs a request on the current connection, logging why if it fails (Value is default then)
    private async Task<(bool Ok, T? Value)> TryRequestAsync<T>(string what, Func<ObsClient, Task<T>> request)
    {
        var client = _client;
        if (client == null)
        {
            AppLog.WriteError($"can't {what}, obs not connected");
            return (false, default);
        }

        try
        {
            return (true, await request(client));
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't {what} ({ex.Message})");
            return (false, default);
        }
    }



    private static async Task<string> CurrentSceneAsync(ObsClient client)
    {
        var data = await client.RequestAsync("GetCurrentProgramScene", null);
        return data.GetProperty("currentProgramSceneName").GetString()!;
    }



    private static async Task<string> SceneOrCurrentAsync(ObsClient client, string scene) =>
        string.IsNullOrWhiteSpace(scene) ? await CurrentSceneAsync(client) : scene.Trim();



    // OBS addresses a source in a scene by a number, looked up from its name
    private static async Task<(string Scene, int Id)> SceneItemAsync(ObsClient client, string scene, string source)
    {
        var sceneName = await SceneOrCurrentAsync(client, scene);
        var data = await client.RequestAsync("GetSceneItemId", new JsonObject { ["sceneName"] = sceneName, ["sourceName"] = source.Trim() });
        return (sceneName, data.GetProperty("sceneItemId").GetInt32());
    }



    private static async Task<(string Scene, int Id, JsonElement Transform)> TransformAsync(ObsClient client, string scene, string source)
    {
        var (sceneName, id) = await SceneItemAsync(client, scene, source);
        var data = await client.RequestAsync("GetSceneItemTransform", new JsonObject { ["sceneName"] = sceneName, ["sceneItemId"] = id });
        return (sceneName, id, data.GetProperty("sceneItemTransform"));
    }



    // a setting as stored, else its default for this kind of source, else null
    private static async Task<JsonElement?> SourceSettingAsync(ObsClient client, string source, string key)
    {
        var data = await client.RequestAsync("GetInputSettings", new JsonObject { ["inputName"] = source.Trim() });
        if (data.GetProperty("inputSettings").TryGetProperty(key, out var value)) return value;

        var defaults = await client.RequestAsync("GetInputDefaultSettings", new JsonObject { ["inputKind"] = data.GetProperty("inputKind").GetString() });
        return defaults.GetProperty("defaultInputSettings").TryGetProperty(key, out var fallback) ? fallback : null;
    }



    // a value as json of the kind the setting already is (null kind: as it reads, a number or true/false if it is one)
    public static JsonNode? ToJson(object value, JsonValueKind? existing)
    {
        var text = value switch
        {
            bool b => b ? "true" : "false",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };

        switch (existing)
        {
            case JsonValueKind.String:
                return JsonValue.Create(text);
            case JsonValueKind.True or JsonValueKind.False:
                return bool.TryParse(text, out var flag) ? JsonValue.Create(flag) : throw new ObsRequestException($"'{text}' isn't true or false");
            case JsonValueKind.Number:
                return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                    ? NumberJson(number) : throw new ObsRequestException($"'{text}' isn't a number");
        }

        if (value is bool or int or double) return value is bool flagValue ? JsonValue.Create(flagValue) : NumberJson(Convert.ToDouble(value));
        if (bool.TryParse(text, out var parsedFlag)) return JsonValue.Create(parsedFlag);
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return NumberJson(parsed);
        return JsonValue.Create(text);
    }



    // whole numbers as whole (OBS treats some settings as integers)
    private static JsonNode NumberJson(double number) =>
        number == Math.Truncate(number) && Math.Abs(number) < long.MaxValue ? JsonValue.Create((long)number) : JsonValue.Create(number);



    // status



    // can be called from any thread, listeners always get it on the UI thread
    private void SetStatus(ConnectionStatus status, string message) => Dispatcher.UIThread.Post(() =>
    {
        if (status == Status && message == StatusMessage) return;

        Status = status;
        StatusMessage = message;
        StatusChanged?.Invoke();
    });



    // disposal



    public void Dispose()
    {
        _model.ConnectionChanged -= OnConnectionChanged;
        _run?.Cancel();
        _run?.Dispose();
        _run = null;
        _client?.Dispose();
        _client = null;
    }
}
