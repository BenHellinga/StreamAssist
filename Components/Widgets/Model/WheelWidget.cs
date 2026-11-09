using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// a slice of the wheel as it's drawn: its option and where it starts and ends, in degrees clockwise from the top
public sealed record WheelSlice(string Text, string Color, double Start, double End);



// one spin, decided when it starts: the winner, and the wheel's rotation going from From to To degrees over Length,
// slowing down as it goes (Ease). The slices are kept as they were, so changing the options mid-spin doesn't change it
public sealed record WheelSpin(IReadOnlyList<WheelSlice> Slices, int WinnerIndex, double From, double To, TimeSpan Length, DateTime StartedAt)
{
    public string Winner => Slices[WinnerIndex].Text;

    public double RotationAt(TimeSpan elapsed) =>
        elapsed >= Length ? To : From + (To - From) * WheelWidget.Ease(elapsed.TotalSeconds / Length.TotalSeconds);
}



// a wheel of options that spins (its header's Spin, or the Spin Wheel block) and lands on one, picked at random by
// weight when the spin starts: fast at first, slowing to a stop, ticking as each slice passes the pointer at the top,
// then the winner shows in front of the darkened wheel for a while. Its options are saved; its rotation and spin are
// only kept while the app runs. The WidgetManager times the spin (sounds, then FinishSpin), its pages draw it
public sealed class WheelWidget : WidgetModel
{
    // constants



    public const double MaxWeight = WidgetOptionList.MaxWeight;
    public const double MaxSeconds = 60;

    public static IReadOnlyList<string> Palette => WidgetOptionList.Palette; // the colours new options get in turn



    // private



    private readonly WidgetOptionList _options;
    private double _spinSeconds;
    private double _showSeconds;
    private string _tickSound;
    private string _winSound;
    private double _tickVolume;
    private double _winVolume;
    private string _outputDevice;
    private double _rotation; // where it rests, this session
    private WheelSpin? _spin;
    private Action<string>? _onSpinDone;
    private string _lastWinner;
    private WheelSpin? _lastSpin; // the one that gave the last winner, and when it stopped (a page opened while it shows)
    private DateTime _finishedAt;



    // public



    [JsonIgnore] public override WidgetKind Kind => WidgetKind.Wheel;
    [JsonIgnore] public WidgetOptionList OptionList => _options; // its box's rows
    [JsonIgnore] public IReadOnlyList<WidgetOption> Options => _options.Items; // the empty last row included
    [JsonIgnore] public IReadOnlyList<WidgetOption> FilledOptions => _options.Filled;
    [JsonIgnore] public double SpinSeconds => _spinSeconds;
    [JsonIgnore] public double ShowSeconds => _showSeconds; // how long the winner shows once it stops
    [JsonIgnore] public string TickSound => _tickSound; // inside Assets/, empty: the built-in tick
    [JsonIgnore] public string WinSound => _winSound; // empty: the built-in chime
    [JsonIgnore] public double TickVolume => _tickVolume; // 0 to 1, 0 = no sound
    [JsonIgnore] public double WinVolume => _winVolume;
    [JsonIgnore] public string OutputDevice => _outputDevice;
    [JsonIgnore] public double Rotation => _rotation;
    [JsonIgnore] public WheelSpin? Spin => _spin; // null while it isn't spinning
    [JsonIgnore] public bool IsSpinning => _spin != null;
    [JsonIgnore] public string LastWinner => _lastWinner; // empty until it has spun this session
    [JsonIgnore] public WheelSpin? LastSpin => _lastSpin;
    [JsonIgnore] public DateTime FinishedAt => _finishedAt;



    // json



    [JsonInclude] [JsonPropertyName("options")]
    private List<WidgetOptionData> JsonOptions
    {
        get => _options.GetData();
        set => _options.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("spinSeconds")]
    private double JsonSpinSeconds
    {
        get => _spinSeconds;
        set => _spinSeconds = value is >= 1 and <= MaxSeconds ? value : 6;
    }

    [JsonInclude] [JsonPropertyName("showSeconds")]
    private double JsonShowSeconds
    {
        get => _showSeconds;
        set => _showSeconds = value is >= 0 and <= MaxSeconds ? value : 4;
    }

    [JsonInclude] [JsonPropertyName("tickSound")]
    private string JsonTickSound
    {
        get => _tickSound;
        set => _tickSound = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("winSound")]
    private string JsonWinSound
    {
        get => _winSound;
        set => _winSound = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("tickVolume")]
    private double JsonTickVolume
    {
        get => _tickVolume;
        set => _tickVolume = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("winVolume")]
    private double JsonWinVolume
    {
        get => _winVolume;
        set => _winVolume = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("outputDevice")]
    private string JsonOutputDevice
    {
        get => _outputDevice;
        set => _outputDevice = value ?? "Default";
    }



    // events



    public event Action? OptionsChanged; // one was added, removed, moved or changed (saved)
    public event Action? SpinStarted;
    public event Action? SpinFinished;



    // constructor



    public WheelWidget()
    {
        _options = new("wheel option", "wheel", () => Name);
        _options.Changed += OnOptionsChanged;
        _spinSeconds = 6;
        _showSeconds = 4;
        _tickSound = "";
        _winSound = "";
        _tickVolume = 0.5;
        _winVolume = 0.5;
        _outputDevice = "Default";
        _rotation = 0;
        _spin = null;
        _onSpinDone = null;
        _lastWinner = "";
        _lastSpin = null;
        _finishedAt = DateTime.MinValue;
    }



    // public api: options (also while the wheel is off; see WidgetOptionList)



    public bool TryAddOption(string text, double weight = 1, string color = "") => _options.TryAdd(text, weight, color);
    public bool TryRemoveOption(string text) => _options.TryRemove(text);
    public void ClearOptions() => _options.Clear();
    public bool TrySetOptionWeight(WidgetOption option, double weight) => _options.TrySetWeight(option, weight);
    public bool TrySetOptionColor(WidgetOption option, string color) => _options.TrySetColor(option, color);
    public void SetOptionText(WidgetOption option, string text) => _options.SetText(option, text);
    public WidgetOption? FindOption(string text) => _options.Find(text);



    // public api: settings



    public bool TrySetSpinSeconds(double seconds) => TrySetSeconds(seconds, 1, ref _spinSeconds, "spin time");
    public bool TrySetShowSeconds(double seconds) => TrySetSeconds(seconds, 0, ref _showSeconds, "winner time");



    public void SetTickSound(string path) => SetText(ref _tickSound, path.Trim());
    public void SetWinSound(string path) => SetText(ref _winSound, path.Trim());
    public void SetOutputDevice(string device) => SetText(ref _outputDevice, device.Trim() == "" ? "Default" : device.Trim());



    public bool TrySetTickVolume(double volume) => TrySetVolume(volume, ref _tickVolume, "tick");
    public bool TrySetWinVolume(double volume) => TrySetVolume(volume, ref _winVolume, "win");



    // public api: spinning



    // starts a spin, picking the winner now. False with an error when there's nothing to spin; false and no error
    // (quietly nothing) while it's off, spinning, or still showing its last winner. onDone gets the winner once it stops
    public bool TryStartSpin(Action<string>? onDone, out string error, DateTime? now = null)
    {
        error = "";
        if (IsDisabled || IsSpinning || IsShowingWinner(now ?? DateTime.UtcNow)) return false;

        var slices = Slices(FilledOptions);
        if (slices.Count == 0)
        {
            error = $"wheel '{Name}' has no options";
            return false;
        }

        var options = FilledOptions;
        var total = options.Sum(o => o.Weight);
        var pick = Random.Shared.NextDouble() * total;
        var winner = 0;
        for (var sum = 0.0; winner < options.Count - 1; winner++)
        {
            sum += options[winner].Weight;
            if (pick < sum) break;
        }

        // lands somewhere inside the winner's slice (not on its edge), after a few whole turns
        var slice = slices[winner];
        var landing = slice.Start + (slice.End - slice.Start) * (0.15 + 0.7 * Random.Shared.NextDouble());
        var turns = Math.Max(3, Math.Round(_spinSeconds * 1.2));
        var to = _rotation + turns * 360 + Mod(-landing - _rotation, 360);

        _spin = new(slices, winner, _rotation, to, TimeSpan.FromSeconds(_spinSeconds), now ?? DateTime.UtcNow);
        _onSpinDone = onDone;
        SpinStarted?.Invoke();
        RaiseDrawChanged();
        return true;
    }



    // the last spin's winner is still showing (its Winner Time since it stopped): a new spin waits for it
    public bool IsShowingWinner(DateTime now) => _lastSpin != null && (now - _finishedAt).TotalSeconds < _showSeconds;



    // the spin has stopped (the WidgetManager, once its time is up): it rests there, showing its winner. Gives back the
    // winner's callback (Spin Wheel's When Done), for the WidgetManager to run once the winner has shown for its time
    public Action? FinishSpin()
    {
        if (_spin is not { } spin) return null;

        var onDone = _onSpinDone;
        _rotation = Mod(spin.To, 360);
        _lastWinner = spin.Winner;
        _lastSpin = spin;
        _finishedAt = DateTime.UtcNow;
        _spin = null;
        _onSpinDone = null;
        SpinFinished?.Invoke();
        RaiseDrawChanged();
        return onDone == null ? null : () => onDone(spin.Winner);
    }



    // helpers: the wheel's geometry (the same maths its pages use)



    // the slices in order, clockwise from the top, each as wide as its share of the weights
    public static List<WheelSlice> Slices(IReadOnlyList<WidgetOption> options)
    {
        var total = options.Sum(o => o.Weight);
        var slices = new List<WheelSlice>();
        if (total <= 0) return slices;

        var start = 0.0;
        foreach (var option in options)
        {
            var end = start + option.Weight / total * 360;
            slices.Add(new(option.Text, option.Color, start, end));
            start = end;
        }
        return slices;
    }



    // fast at first, slowing to a stop: 1 - (1 - x)^4
    public static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 4);



    // when, into a spin, each slice edge passes the pointer (the ticks), at most one every minGap
    public static List<TimeSpan> TickTimes(WheelSpin spin, TimeSpan minGap)
    {
        var times = new List<TimeSpan>();
        var edges = spin.Slices.Select(s => s.Start).ToList();
        TimeSpan? last = null;

        // the pointer is at -rotation: an edge e passes it when the rotation reaches -e (mod 360)
        var crossings = new List<double>();
        foreach (var edge in edges)
        {
            var first = spin.From + Mod(-edge - spin.From, 360);
            if (first <= spin.From) first += 360;
            for (var at = first; at <= spin.To; at += 360) crossings.Add(at);
        }

        foreach (var at in crossings.Order())
        {
            // the rotation is From + (To - From) * (1 - (1 - x)^4): solved for x
            var progress = (at - spin.From) / (spin.To - spin.From);
            var x = 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), 0.25);
            var time = TimeSpan.FromSeconds(x * spin.Length.TotalSeconds);
            if (last is { } previous && time - previous < minGap) continue;
            times.Add(time);
            last = time;
        }
        return times;
    }



    private static double Mod(double value, double by) => ((value % by) + by) % by;



    private bool TrySetSeconds(double seconds, double min, ref double field, string what)
    {
        if (!(seconds >= min && seconds <= MaxSeconds))
        {
            AppLog.WriteError($"can't set wheel {what} to {seconds}, it's {min} to {MaxSeconds} seconds");
            return false;
        }

        if (seconds != field)
        {
            field = seconds;
            RaiseSettingsChanged();
        }
        return true;
    }



    private bool TrySetVolume(double volume, ref double field, string what)
    {
        if (!(volume is >= 0 and <= 1))
        {
            AppLog.WriteError($"can't set wheel {what} volume to {volume}, it's 0 to 1");
            return false;
        }

        if (volume != field)
        {
            field = volume;
            RaiseSettingsChanged();
        }
        return true;
    }



    private void SetText(ref string field, string value)
    {
        if (value == field) return;
        field = value;
        RaiseSettingsChanged();
    }



    // model callbacks



    private void OnOptionsChanged()
    {
        OptionsChanged?.Invoke();
        RaiseSettingsChanged();
    }



    // disposal



    public override void Dispose()
    {
        _spin = null;
        _onSpinDone = null;
        _options.Changed -= OnOptionsChanged;
        _options.Dispose();
    }
}
