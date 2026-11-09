using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Widgets.Model;



// what a widget is (the widget list's cards, in this order), saved by name
[JsonConverter(typeof(JsonStringEnumConverter<WidgetKind>))]
public enum WidgetKind { MicBounce, Wheel, Plinko }



// one widget on the Widgets tab: something drawn on stream by its own browser source in OBS
// (http://127.0.0.1:<port>/widget/<name>, served by the WidgetManager), with its own settings. Its name is what blocks
// and its address use (unique in its config). Turned off, it doesn't run (its page shows nothing, no sound, no spins,
// no listening), but its settings can still be changed, from its box or by blocks. Hidden, it still runs (spins, sounds,
// its preview), its page just draws nothing, so it can be tested off stream.
// Saved with its kind first ("kind": "Wheel"), so it loads as the right one
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(MicBounceWidget), "MicBounce")]
[JsonDerivedType(typeof(WheelWidget), "Wheel")]
[JsonDerivedType(typeof(PlinkoWidget), "Plinko")]
public abstract class WidgetModel : IDisposable
{
    // private



    private string _id;
    private string _name;
    private bool _isDisabled;
    private bool _isHidden;
    private bool _isCollapsed;



    // public



    [JsonIgnore] public string Id => _id;
    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public bool IsHidden => _isHidden;
    [JsonIgnore] public bool IsCollapsed => _isCollapsed;
    [JsonIgnore] public abstract WidgetKind Kind { get; }



    // json



    [JsonInclude] [JsonPropertyName("id")]
    private string JsonId
    {
        get => _id;
        set => _id = value;
    }

    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("isDisabled")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("isHidden")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private bool JsonIsHidden
    {
        get => _isHidden;
        set => _isHidden = value;
    }

    [JsonInclude] [JsonPropertyName("isCollapsed")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private bool JsonIsCollapsed
    {
        get => _isCollapsed;
        set => _isCollapsed = value;
    }



    // events



    public event Action? NameChanged;
    public event Action<bool>? IsDisabledChanged;
    public event Action<bool>? IsHiddenChanged;
    public event Action<bool>? IsCollapsedChanged;
    public event Action? SettingsChanged; // something its page draws changed (its pages and preview draw it again)
    public event Action? Changed; // anything saved changed



    // constructor



    protected WidgetModel()
    {
        _id = Guid.NewGuid().ToString("N");
        _name = "";
        _isDisabled = false;
        _isHidden = false;
        _isCollapsed = false;
    }



    public static WidgetModel Create(WidgetKind kind, string name)
    {
        WidgetModel widget = kind switch
        {
            WidgetKind.Wheel => new WheelWidget(),
            WidgetKind.Plinko => new PlinkoWidget(),
            _ => new MicBounceWidget(),
        };
        widget._name = name;
        return widget;
    }



    // a copy of a widget (pasted), the same way it's saved, under another id and name (null if it can't be read back)
    public static WidgetModel? CopyOf(WidgetModel widget, string name)
    {
        try
        {
            var copy = JsonSerializer.Deserialize<WidgetModel>(JsonSerializer.Serialize(widget));
            if (copy == null) return null;

            copy._id = Guid.NewGuid().ToString("N");
            copy._name = name;
            return copy;
        }
        catch (JsonException)
        {
            return null;
        }
    }



    // how a kind is shown (its card in the widget list, a new widget's name)
    public static string KindTitle(WidgetKind kind) => kind switch
    {
        WidgetKind.MicBounce => "Mic Bounce",
        _ => kind.ToString(),
    };



    // public api (names are checked by the config, which knows the other widgets)



    internal void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        NameChanged?.Invoke();
        Changed?.Invoke();
    }



    // on / off: off, it doesn't run (an empty page, no sound or spins), its settings still change
    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(isDisabled);
        SettingsChanged?.Invoke();
        Changed?.Invoke();
    }



    // shown / hidden on stream: hidden, its page draws nothing but it still runs
    public void SetIsHidden(bool isHidden)
    {
        if (isHidden == _isHidden) return;

        _isHidden = isHidden;
        IsHiddenChanged?.Invoke(isHidden);
        SettingsChanged?.Invoke();
        Changed?.Invoke();
    }



    public void SetIsCollapsed(bool isCollapsed)
    {
        if (isCollapsed == _isCollapsed) return;

        _isCollapsed = isCollapsed;
        IsCollapsedChanged?.Invoke(isCollapsed);
        Changed?.Invoke();
    }



    // protected



    // a setting changed: drawn again and saved
    protected void RaiseSettingsChanged()
    {
        SettingsChanged?.Invoke();
        Changed?.Invoke();
    }



    // drawn again, not saved (e.g. a wheel spun)
    protected void RaiseDrawChanged() => SettingsChanged?.Invoke();



    // disposal



    public virtual void Dispose() { }
}
