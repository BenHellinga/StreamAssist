using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// an image that rises as you talk, higher the louder you are, listening to a microphone input (any of Windows', the
// same list a Microphone box picks from): the idle image while you're quiet, the talking one while it's over Min dB
// (held a moment through gaps between words). How high it is follows your level between Min dB (resting) and Max dB (the
// top, anything louder stays there) on an upside-down parabola: it jumps up quickly from Min and eases into the top
// (Target). Smoothing is how long it takes to follow (0: straight there, every update), see Smooth. The level is
// worked out by the WidgetManager, only while the app runs
public sealed class MicBounceWidget : WidgetModel
{
    // constants



    public const double MaxHeight = 2000; // pixels
    public const double MinLevel = -100; // dB, the quietest Min / Max can be (0 is full scale)
    public const double MaxSmoothing = 2000; // ms
    private const double TalkingHoldMs = 200; // the talking image stays this long after it's quiet again



    // private



    private string _idleImage;
    private string _talkingImage;
    private string? _microphone; // the microphone input, by its id (InputDeviceModel.DevicePath)
    private double _height;
    private double _minDb;
    private double _maxDb;
    private double _smoothing;
    private double _level; // 0 (resting) to 1 (Height up), this session
    private bool _isTalking;
    private double _quietMs; // how long it's been under Min, while talking



    // public



    [JsonIgnore] public override WidgetKind Kind => WidgetKind.MicBounce;
    [JsonIgnore] public string IdleImage => _idleImage; // inside Assets/, like Play Sound's file
    [JsonIgnore] public string TalkingImage => _talkingImage; // empty: the idle one while talking too
    [JsonIgnore] public string? Microphone => _microphone;
    [JsonIgnore] public double Height => _height; // pixels at full loudness
    [JsonIgnore] public double MinDb => _minDb; // where it starts to rise
    [JsonIgnore] public double MaxDb => _maxDb; // where it's at the top
    [JsonIgnore] public double Smoothing => _smoothing; // ms to follow a change (0: straight there)
    [JsonIgnore] public double Level => _level;
    [JsonIgnore] public bool IsTalking => _isTalking;
    [JsonIgnore] public string ShownImage => _isTalking && _talkingImage != "" ? _talkingImage : _idleImage;



    // json



    [JsonInclude] [JsonPropertyName("idleImage")]
    private string JsonIdleImage
    {
        get => _idleImage;
        set => _idleImage = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("talkingImage")]
    private string JsonTalkingImage
    {
        get => _talkingImage;
        set => _talkingImage = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("microphone")]
    private string? JsonMicrophone
    {
        get => _microphone;
        set => _microphone = value;
    }

    [JsonInclude] [JsonPropertyName("height")]
    private double JsonHeight
    {
        get => _height;
        set => _height = value is >= 0 and <= MaxHeight ? value : 40;
    }

    [JsonInclude] [JsonPropertyName("minDb")]
    private double JsonMinDb
    {
        get => _minDb;
        set => _minDb = value is >= MinLevel and <= 0 ? value : -40;
    }

    [JsonInclude] [JsonPropertyName("maxDb")]
    private double JsonMaxDb
    {
        get => _maxDb;
        set => _maxDb = value is >= MinLevel and <= 0 ? value : -10;
    }

    [JsonInclude] [JsonPropertyName("smoothing")]
    private double JsonSmoothing
    {
        get => _smoothing;
        set => _smoothing = value is >= 0 and <= MaxSmoothing ? value : 50;
    }



    // events



    public event Action? LiveChanged; // its level or talking changed (this session, many times a second)



    // constructor



    public MicBounceWidget()
    {
        _idleImage = "";
        _talkingImage = "";
        _microphone = null;
        _height = 40;
        _minDb = -40;
        _maxDb = -10;
        _smoothing = 50;
        _level = 0;
        _isTalking = false;
    }



    // public api



    public void SetIdleImage(string path)
    {
        if (path.Trim() == _idleImage) return;
        _idleImage = path.Trim();
        RaiseSettingsChanged();
    }



    public void SetTalkingImage(string path)
    {
        if (path.Trim() == _talkingImage) return;
        _talkingImage = path.Trim();
        RaiseSettingsChanged();
    }



    public void SetMicrophone(string? inputId)
    {
        if (inputId == _microphone) return;
        _microphone = inputId;
        RaiseSettingsChanged();
    }



    public bool TrySetHeight(double height)
    {
        if (!(height is >= 0 and <= MaxHeight))
        {
            AppLog.WriteError($"can't set bounce height to {height}, it's 0 to {MaxHeight} pixels");
            return false;
        }

        if (height != _height)
        {
            _height = height;
            RaiseSettingsChanged();
        }
        return true;
    }



    // Min below Max (both -100 to 0 dB): set together, so either can move past where the other was
    public bool TrySetRange(double minDb, double maxDb)
    {
        if (!(minDb >= MinLevel && maxDb <= 0 && minDb < maxDb))
        {
            AppLog.WriteError($"can't set bounce range to {minDb} to {maxDb} dB, it's {MinLevel} to 0 with min below max");
            return false;
        }

        if ((minDb != _minDb || maxDb != _maxDb))
        {
            _minDb = minDb;
            _maxDb = maxDb;
            RaiseSettingsChanged();
        }
        return true;
    }



    public bool TrySetSmoothing(double milliseconds)
    {
        if (!(milliseconds is >= 0 and <= MaxSmoothing))
        {
            AppLog.WriteError($"can't set bounce smoothing to {milliseconds}, it's 0 to {MaxSmoothing} ms");
            return false;
        }

        if (milliseconds != _smoothing)
        {
            _smoothing = milliseconds;
            RaiseSettingsChanged();
        }
        return true;
    }



    // the WidgetManager's tick: the microphone's level, ms since the last one
    public void Update(double levelDb, double elapsedMs)
    {
        _quietMs = levelDb > _minDb ? 0 : _quietMs + elapsedMs;
        var isTalking = levelDb > _minDb || _isTalking && _quietMs < TalkingHoldMs;
        var level = Smooth(_level, Target(levelDb, _minDb, _maxDb), elapsedMs, _smoothing);
        if (Math.Abs(level - _level) < 0.0005 && isTalking == _isTalking) return;

        _level = level;
        _isTalking = isTalking;
        LiveChanged?.Invoke();
    }



    // how far up (0 to 1) a level is: nothing up to Min, the top from Max, and between them 1 - (1 - x)^2 (x the way
    // from Min to Max): an upside-down parabola, rising fastest at Min and flattening into the top
    public static double Target(double levelDb, double minDb, double maxDb)
    {
        var x = Math.Clamp((levelDb - minDb) / (maxDb - minDb), 0, 1);
        return 1 - (1 - x) * (1 - x);
    }



    // moves towards the target, taking about smoothingMs to get most of the way (0: straight there)
    public static double Smooth(double level, double target, double elapsedMs, double smoothingMs) =>
        smoothingMs <= 0 ? target : level + (target - level) * (1 - Math.Exp(-elapsedMs / smoothingMs));
}
