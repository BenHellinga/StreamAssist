using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using NAudio.Wave;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Widgets.Background;



// runs the widgets: the server their pages come from (started once at startup, or on Start / Retry on the Connections
// tab, a failure logged once), what each page is told to draw (everything when it connects, then each change), each
// Mic Bounce's level from its microphone box (50 times a second), and each spin's timing: the ticks as slices pass the
// pointer, the win sound when it stops, then FinishSpin; and each plinko board's physics (stepped 50 times a second while
// it has balls, which are sent to its pages each time), its pin sounds and a finished drop's win sound. Sounds play through the app (the
// widget's output device), not OBS. Only the config in use's widgets are drawn; a spin that started carries on to its
// end whatever happens to the tab
public sealed class WidgetManager : IDisposable
{
    // constants



    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan MinTickGap = TimeSpan.FromMilliseconds(45); // faster than this, ticks are skipped



    // private



    private readonly WidgetsComponentModel _model;
    private readonly ThemeComponentModel? _theme; // its WheelMinTextSize goes to the wheels' pages
    private readonly MicrophoneManager? _microphones; // listens to the inputs (none in the tests)
    private readonly WidgetServer _server;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock;
    private readonly Dictionary<WheelWidget, CancellationTokenSource> _spins;
    private readonly Dictionary<PlinkoWidget, PlinkoSounds> _plinkos; // the boards with balls on them, and their sounds
    private WidgetsConfigModel? _config; // the one listened to
    private readonly List<WidgetModel> _widgets; // its widgets, listened to
    private TimeSpan _lastTick;



    // public



    public ConnectionStatus Status { get; private set; }
    public string StatusMessage { get; private set; }



    // events



    public event Action? StatusChanged;



    // constructor



    public WidgetManager(WidgetsComponentModel model, MicrophoneManager? microphones, ThemeComponentModel? theme = null)
    {
        _model = model;
        _theme = theme;
        _microphones = microphones;
        _server = new(_ => WidgetPage.Html, ReadAsset);
        _clock = Stopwatch.StartNew();
        _spins = [];
        _plinkos = [];
        _widgets = [];
        Status = ConnectionStatus.NotConnected;
        StatusMessage = "Not running";

        _model.ServerChanged += OnServerChanged;
        _model.Configs.ListChanged += OnConfigsChanged;
        _server.Joined += OnPageJoined;
        if (_theme != null) _theme.ValueChanged += OnThemeValueChanged;

        _timer = new DispatcherTimer { Interval = Tick };
        _timer.Tick += OnTick;
        _timer.Start();

        ListenToConfig();
        if (_model.IsEnabled) Start();
        if (_microphones != null) _microphones.ExtraInputs = WantedInputs;
    }



    // public api



    // a widget's address, for OBS (127.0.0.1 rather than localhost: browsers may try IPv6 first, which isn't served)
    public static string UrlOf(int port, string name) => $"http://127.0.0.1:{port}/widget/{Uri.EscapeDataString(name)}";



    // the server



    private void Start()
    {
        if (_server.TryStart(_model.Port, out var error))
        {
            SetStatus(ConnectionStatus.Connected, $"Running on port {_model.Port}");
            return;
        }

        SetStatus(ConnectionStatus.Failed, Capitalize(error));
        AppLog.WriteError($"can't start widget server ({error})");
    }



    private void OnServerChanged()
    {
        _server.Stop();
        SetStatus(ConnectionStatus.NotConnected, "Not running");
        if (_model.IsEnabled) Start();
    }



    private void SetStatus(ConnectionStatus status, string message)
    {
        Status = status;
        StatusMessage = message;
        StatusChanged?.Invoke();
    }



    // a page connected (on the server's thread): it gets everything
    private void OnPageJoined(string name) => Dispatcher.UIThread.Post(() => SendState(name));



    private static (byte[] Data, string Type)? ReadAsset(string path)
    {
        if (!AppPaths.TryResolveAsset(path, out var full, out _) || !File.Exists(full)) return null;

        try
        {
            var type = Path.GetExtension(full).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream",
            };
            return (File.ReadAllBytes(full), type);
        }
        catch (Exception)
        {
            return null;
        }
    }



    // what pages are told



    private void SendState(string name)
    {
        var widget = _config?.FindWidget(name);
        _server.Send(name, (widget == null ? new JsonObject { ["type"] = "state", ["found"] = false } : StateOf(widget)).ToJsonString());
    }



    private void SendAllStates()
    {
        foreach (var name in _server.OpenNames()) SendState(name);
    }



    private static JsonObject StateOf(WidgetModel widget)
    {
        var state = new JsonObject
        {
            ["type"] = "state",
            ["found"] = true,
            ["kind"] = widget.Kind.ToString(),
            ["disabled"] = widget.IsDisabled,
            ["hidden"] = widget.IsHidden,
        };

        switch (widget)
        {
            case MicBounceWidget bounce:
                state["idleUrl"] = AssetUrl(bounce.IdleImage);
                state["talkingUrl"] = AssetUrl(bounce.TalkingImage);
                state["height"] = bounce.Height;
                state["smoothing"] = bounce.Smoothing;
                state["level"] = bounce.Level;
                state["isTalking"] = bounce.IsTalking;
                break;

            case WheelWidget wheel:
                state["slices"] = SlicesJson(WheelWidget.Slices(wheel.FilledOptions));
                state["rotation"] = wheel.Rotation;
                state["minTextSize"] = WheelMinTextSize();
                state["spin"] = wheel.Spin is { } spin ? SpinJson(wheel, spin) : null;

                // a winner still showing (a page opened, or OBS reloaded it, after the spin stopped)
                var shown = (DateTime.UtcNow - wheel.FinishedAt).TotalMilliseconds;
                if (wheel.LastSpin is { } last && wheel.Spin == null && shown < wheel.ShowSeconds * 1000)
                    state["showing"] = new JsonObject
                    {
                        ["slices"] = SlicesJson(last.Slices),
                        ["winnerIndex"] = last.WinnerIndex,
                        ["elapsedMs"] = shown,
                        ["showMs"] = wheel.ShowSeconds * 1000,
                    };
                break;

            case PlinkoWidget plinko:
                var board = plinko.Board;
                state["board"] = new JsonObject
                {
                    ["pins"] = new JsonArray([.. board.Pins().SelectMany(p => new JsonNode[] { p.X, p.Y })]),
                    ["pinRadius"] = board.PinRadius,
                    ["ballRadius"] = board.BallRadius,
                    ["walls"] = new JsonArray([.. board.Drawn.Select(w => (JsonNode)new JsonArray(w.X1, w.Y1, w.X2, w.Y2))]),
                    ["buckets"] = new JsonArray([.. board.Buckets.Select(b => (JsonNode)new JsonObject
                        { ["text"] = b.Text, ["color"] = b.Color, ["left"] = b.Left, ["right"] = b.Right })]),
                    ["bucketTop"] = board.BucketTop,
                    ["floor"] = board.Floor,
                    ["bounds"] = new JsonArray(board.Left, board.Top, board.Right, board.Bottom),
                    ["resultY"] = board.ResultY,
                    ["labelMax"] = PlinkoBoard.LabelMax,
                    ["labelMin"] = PlinkoBoard.LabelMin,
                };
                state["frame"] = FrameJson(plinko.Frame());
                break;
        }
        return state;
    }



    // the balls (id, x, y, colour, flattened), how faded they are, each bucket's count, the result showing
    private static JsonObject FrameJson(PlinkoFrame frame) => new()
    {
        ["type"] = "plinko",
        ["balls"] = new JsonArray([.. frame.Balls.SelectMany(b => new JsonNode?[] { b.Id, Math.Round(b.X, 3), Math.Round(b.Y, 3), b.Color })]),
        ["alpha"] = frame.Alpha,
        ["counts"] = new JsonArray([.. frame.Counts.Select(c => (JsonNode)c)]),
        ["result"] = frame.Result is { } result ? new JsonObject { ["text"] = result.Text, ["color"] = result.Color, ["alpha"] = Math.Round(result.Alpha, 3) } : null,
    };



    private static JsonObject SpinJson(WheelWidget wheel, WheelSpin spin) => new()
    {
        ["type"] = "spin",
        ["from"] = spin.From,
        ["to"] = spin.To,
        ["lengthMs"] = spin.Length.TotalMilliseconds,
        ["elapsedMs"] = Math.Max(0, (DateTime.UtcNow - spin.StartedAt).TotalMilliseconds),
        ["slices"] = SlicesJson(spin.Slices),
        ["winnerIndex"] = spin.WinnerIndex,
        ["showMs"] = wheel.ShowSeconds * 1000,
    };



    private static JsonArray SlicesJson(IEnumerable<WheelSlice> slices) =>
        [.. slices.Select(s => (JsonNode)new JsonObject { ["text"] = s.Text, ["color"] = s.Color, ["start"] = s.Start, ["end"] = s.End })];



    // the Themes tab's smallest slice text (Sizes.axaml WheelMinTextSize), a % of the wheel's radius
    private static double WheelMinTextSize() =>
        Avalonia.Application.Current is { } app && Avalonia.Controls.ResourceNodeExtensions.TryFindResource(app, "WheelMinTextSize", out var found)
        && found is double size ? size : 4;



    // a theme number changed: the wheels' pages draw with the new min text size
    private void OnThemeValueChanged(string key)
    {
        if (key == "WheelMinTextSize") SendAllStates();
    }



    // a path inside Assets/ as the server's address for it (empty stays empty)
    private static string AssetUrl(string path) =>
        path.Trim() == "" ? "" : "/asset/" + string.Join("/", path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));



    // listening to the config in use and its widgets



    private void OnConfigsChanged()
    {
        ListenToConfig();
        SendAllStates();
    }



    private void ListenToConfig()
    {
        if (_config != null)
        {
            _config.WidgetAdded -= OnWidgetAdded;
            _config.WidgetRemoved -= OnWidgetRemoved;
        }
        foreach (var widget in _widgets.ToList()) Forget(widget);

        _config = _model.Config;
        if (_config == null) return;

        _config.WidgetAdded += OnWidgetAdded;
        _config.WidgetRemoved += OnWidgetRemoved;
        foreach (var widget in _config.Widgets) Listen(widget);
    }



    private void OnWidgetAdded(WidgetModel widget, int _)
    {
        Listen(widget);
        SendState(widget.Name);
    }



    private void OnWidgetRemoved(WidgetModel widget)
    {
        Forget(widget);
        if (widget is WheelWidget wheel && _spins.Remove(wheel, out var spin)) spin.Cancel(); // nothing left to finish
        SendState(widget.Name);
    }



    private void Listen(WidgetModel widget)
    {
        _widgets.Add(widget);
        widget.SettingsChanged += OnSettingsChanged;
        widget.NameChanged += OnNameChanged;
        if (widget is MicBounceWidget bounce) bounce.LiveChanged += OnLiveChanged;
        if (widget is WheelWidget wheel) wheel.SpinStarted += OnSpinStarted;
        if (widget is PlinkoWidget plinko) plinko.DropStarted += OnDropStarted;
    }



    private void Forget(WidgetModel widget)
    {
        _widgets.Remove(widget);
        widget.SettingsChanged -= OnSettingsChanged;
        widget.NameChanged -= OnNameChanged;
        if (widget is MicBounceWidget bounce) bounce.LiveChanged -= OnLiveChanged;
        if (widget is WheelWidget wheel) wheel.SpinStarted -= OnSpinStarted;
        if (widget is PlinkoWidget plinko)
        {
            plinko.DropStarted -= OnDropStarted;
            _plinkos.Remove(plinko);
        }
    }



    // the events say which widget only by firing, so every widget's page is told again (cheap: a few small messages)
    private void OnSettingsChanged() => SendAllStates();
    private void OnNameChanged() => SendAllStates(); // its old address shows nothing now, its new one shows it



    private void OnLiveChanged()
    {
        foreach (var bounce in _widgets.OfType<MicBounceWidget>())
            _server.Send(bounce.Name, new JsonObject { ["type"] = "live", ["level"] = bounce.Level, ["isTalking"] = bounce.IsTalking }.ToJsonString());
    }



    // mic bounce: each widget's input level, 50 times a second



    // the inputs the config in use's Mic Bounce widgets listen to (the MicrophoneManager asks)
    private IEnumerable<string> WantedInputs() =>
        _widgets.OfType<MicBounceWidget>().Where(b => !b.IsDisabled && b.Microphone != null).Select(b => b.Microphone!).ToList();



    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = (now - _lastTick).TotalMilliseconds;
        _lastTick = now;

        foreach (var bounce in _widgets.OfType<MicBounceWidget>().ToList())
        {
            var level = bounce.IsDisabled || bounce.Microphone == null || _microphones == null ? MicrophoneManager.Silence : _microphones.LevelOf(bounce.Microphone);
            bounce.Update(level, elapsed);
        }

        StepPlinkos(elapsed / 1000);
    }



    // wheel: timing a spin



    private void OnSpinStarted()
    {
        foreach (var wheel in _widgets.OfType<WheelWidget>().Where(w => w.Spin != null && !_spins.ContainsKey(w)).ToList())
        {
            var spin = wheel.Spin!;
            _server.Send(wheel.Name, SpinJson(wheel, spin).ToJsonString());

            var cancel = new CancellationTokenSource();
            _spins[wheel] = cancel;
            _ = RunSpinAsync(wheel, spin, cancel.Token);
        }
    }



    // ticks as each slice passes, the win sound once it stops (the wheel finishes, showing its winner), then once the
    // winner has shown for its time, Spin Wheel's When Done
    private async Task RunSpinAsync(WheelWidget wheel, WheelSpin spin, CancellationToken ct)
    {
        try
        {
            var sounds = SoundsFor(wheel);
            var started = Stopwatch.StartNew();

            foreach (var at in WheelWidget.TickTimes(spin, MinTickGap))
            {
                var wait = at - started.Elapsed;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                sounds.Tick?.Invoke();
            }

            var left = spin.Length - started.Elapsed;
            if (left > TimeSpan.Zero) await Task.Delay(left, ct);

            sounds.Win?.Invoke();
            _spins.Remove(wheel);
            var done = wheel.FinishSpin();

            if (done == null) return;
            if (wheel.ShowSeconds > 0) await Task.Delay(TimeSpan.FromSeconds(wheel.ShowSeconds), ct);
            done();
        }
        catch (OperationCanceledException)
        {
            // the wheel was removed, or the app is closing
        }
    }



    // how this spin plays its sounds: checked once (a missing file or device is said once, then that sound is left out)
    private static (Action? Tick, Action? Win) SoundsFor(WheelWidget wheel) =>
        Sounds($"wheel '{wheel.Name}'", wheel.OutputDevice, (wheel.TickSound, wheel.TickVolume, "tick"), (wheel.WinSound, wheel.WinVolume, "win"));



    // a widget's two sounds (empty paths: the built-in tick and chime), each null if it's silent or can't play
    private static (Action? Tick, Action? Win) Sounds(string who, string device, (string Path, double Volume, string What) tick, (string Path, double Volume, string What) win)
    {
        if ((tick.Volume > 0 || win.Volume > 0) && !AudioHelper.TryGetDeviceByString(device, out _)) return (null, null);

        Action? Player((string Path, double Volume, string What) sound, Func<WaveStream> builtIn)
        {
            if (sound.Volume <= 0) return null;
            if (sound.Path == "") return () => AudioHelper.TryOpenWave(builtIn(), (float)sound.Volume, device)?.Start(() => { });

            if (!AppPaths.TryResolveAsset(sound.Path, out var full, out var error) || !File.Exists(full))
            {
                AppLog.WriteError($"{who} can't play its {sound.What} sound '{sound.Path}' ({(error == "" ? "no such file" : error)})");
                return null;
            }
            return () => AudioHelper.TryOpenSound(sound.Path, (float)sound.Volume, device)?.Start(() => { });
        }

        return (Player(tick, WheelSounds.Tick), Player(win, WheelSounds.Chime));
    }



    // plinko: stepping the boards with balls on them



    // a board's sounds (checked when a drop starts), and when a pin tick last played (at most once every MinTickGap, so
    // many balls aren't a wall of noise)
    private sealed class PlinkoSounds(Action? pin, Action? win)
    {
        public Action? Pin { get; } = pin;
        public Action? Win { get; } = win;
        public TimeSpan? LastPin { get; set; } // null: not yet (TimeSpan.MinValue would overflow when subtracted)
    }



    private void OnDropStarted()
    {
        foreach (var plinko in _widgets.OfType<PlinkoWidget>().Where(p => p.IsBusy && !_plinkos.ContainsKey(p)).ToList())
        {
            var (pin, win) = Sounds($"plinko '{plinko.Name}'", plinko.OutputDevice, (plinko.PinSound, plinko.PinVolume, "pin"), (plinko.WinSound, plinko.WinVolume, "win"));
            _plinkos[plinko] = new(pin, win);
        }
    }



    // each busy board moves on, its pages get the balls; once there's nothing left they get that once more, and it's let go
    private void StepPlinkos(double seconds)
    {
        var now = _clock.Elapsed;
        foreach (var (plinko, sounds) in _plinkos.ToList())
        {
            var (pins, wins) = plinko.Step(seconds);
            if (pins > 0 && sounds.Pin != null && (sounds.LastPin is not { } pin || now - pin >= MinTickGap))
            {
                sounds.Pin();
                sounds.LastPin = now;
            }
            if (wins > 0) sounds.Win?.Invoke();

            _server.Send(plinko.Name, FrameJson(plinko.Frame()).ToJsonString());
            if (!plinko.IsBusy) _plinkos.Remove(plinko);
        }
    }



    private static string Capitalize(string text) => text == "" ? text : char.ToUpperInvariant(text[0]) + text[1..];



    // disposal



    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        _model.ServerChanged -= OnServerChanged;
        _model.Configs.ListChanged -= OnConfigsChanged;
        if (_theme != null) _theme.ValueChanged -= OnThemeValueChanged;
        _server.Joined -= OnPageJoined;
        if (_config != null)
        {
            _config.WidgetAdded -= OnWidgetAdded;
            _config.WidgetRemoved -= OnWidgetRemoved;
        }
        foreach (var widget in _widgets.ToList()) Forget(widget);
        foreach (var spin in _spins.Values) spin.Cancel();
        _spins.Clear();
        if (_microphones != null) _microphones.ExtraInputs = null;
        _server.Dispose();
    }
}
