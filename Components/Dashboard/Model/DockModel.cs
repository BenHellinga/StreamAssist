using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Dashboard.Model;



// what a dock shows (the dock list's cards, in this order), saved by name. Blank is an empty spot a copied dock is
// pasted into (it takes the blank's place)
[JsonConverter(typeof(JsonStringEnumConverter<DockKind>))]
public enum DockKind { Console, Timer, Sound, Button, Indicator, Variable, Blank }



// one dock on the dashboard: its name (what blocks use to find it, unique in its config) and its kind. Its id ties it
// to its place in the layout and never changes. A console's lines are only kept while the app runs; a button dock's
// indicator and variable docks' rows are saved. A timer dock's timers and an indicator dock's on/off states live here
// too, only while the app runs (blocks name the dock, then the timer or indicator), and a sound dock's queue of sounds. Variable docks show the Data tab's
// variables, which live there. A dock can be turned off (saved, not a blank one): then everything that would change it
// does nothing, quietly (a message, a timer started or changed, an indicator set, a button pressed); reading it still
// works
public sealed class DockModel : IDisposable
{
    // constants



    private const int MaxLines = 500;



    // private



    private string _id;
    private string _name;
    private DockKind _kind;
    private readonly DockButtonList _buttons;
    private readonly DockNameList _indicators;
    private readonly DockNameList _variables;
    private readonly TimersModel _timers;
    private readonly SoundsModel _sounds;
    private readonly Dictionary<string, bool> _indicatorStates; // by indicator name, case ignored
    private bool _isDisabled;



    // public



    [JsonIgnore] public string Id => _id;
    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public DockKind Kind => _kind;
    [JsonIgnore] public ObservableCollection<string> Lines { get; } // a console's messages, "[time] message"
    [JsonIgnore] public DockButtonList Buttons => _buttons; // a button dock's rows
    [JsonIgnore] public DockNameList Indicators => _indicators; // an indicator dock's rows
    [JsonIgnore] public DockNameList Variables => _variables; // a variable dock's rows
    [JsonIgnore] public TimersModel Timers => _timers; // a timer dock's running timers
    [JsonIgnore] public SoundsModel Sounds => _sounds; // a sound dock's playing and waiting sounds
    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public bool CanDisable => _kind != DockKind.Blank;



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

    [JsonInclude] [JsonPropertyName("kind")]
    private DockKind JsonKind
    {
        get => _kind;
        set => _kind = value;
    }

    [JsonInclude] [JsonPropertyName("isDisabled")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set
        {
            _isDisabled = value;
            _timers.SetIsOff(value);
            _sounds.SetIsOff(value);
        }
    }

    // only written for sound docks: play one at a time (the default) or over each other
    [JsonInclude] [JsonPropertyName("waitUntilDone")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private bool? JsonWaitUntilDone
    {
        get => _kind == DockKind.Sound ? _sounds.WaitUntilDone : null;
        set => _sounds.SetWaitUntilDone(value ?? true);
    }

    // only written for button docks
    [JsonInclude] [JsonPropertyName("buttons")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private List<DockButtonData>? JsonButtons
    {
        get => _kind == DockKind.Button ? _buttons.GetData() : null;
        set => _buttons.LoadData(value ?? []);
    }

    // only written for indicator docks: the rows' names
    [JsonInclude] [JsonPropertyName("indicators")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private List<string>? JsonIndicators
    {
        get => _kind == DockKind.Indicator ? _indicators.GetData() : null;
        set => _indicators.LoadData(value ?? []);
    }

    // only written for variable docks: the rows' variable names
    [JsonInclude] [JsonPropertyName("variables")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private List<string>? JsonVariables
    {
        get => _kind == DockKind.Variable ? _variables.GetData() : null;
        set => _variables.LoadData(value ?? []);
    }



    // events



    public event Action? NameChanged;
    public event Action? Changed; // anything saved changed
    public event Action<string>? IndicatorStateChanged; // the indicator's name
    public event Action<bool>? IsDisabledChanged;



    // constructor



    public DockModel()
    {
        _id = Guid.NewGuid().ToString("N");
        _name = "";
        _kind = DockKind.Console;
        Lines = [];
        _buttons = new();
        _indicators = new();
        _variables = new();
        _timers = new();
        _sounds = new();
        _indicatorStates = new(StringComparer.OrdinalIgnoreCase);

        _buttons.Changed += OnButtonsChanged;
        _indicators.Changed += OnIndicatorsChanged;
        _sounds.WaitUntilDoneChanged += OnSoundsWaitUntilDoneChanged;
        _variables.Changed += OnVariablesChanged;
    }



    public static DockModel Create(string name, DockKind kind) => new() { _name = name, _kind = kind };



    // a copy of a dock (pasted), the same way it's saved, under another id and name (null if it can't be read back)
    public static DockModel? CopyOf(DockModel dock, string id, string name)
    {
        try
        {
            var copy = JsonSerializer.Deserialize<DockModel>(JsonSerializer.Serialize(dock));
            if (copy == null) return null;

            copy._id = id;
            copy._name = name;
            return copy;
        }
        catch (JsonException)
        {
            return null;
        }
    }



    // how a kind is shown (its card in the dock list, a new dock's name); the enum names are the saved ones, so they stay
    public static string KindTitle(DockKind kind) => kind switch
    {
        DockKind.Timer => "Timers",
        DockKind.Sound => "Sounds",
        DockKind.Button => "Buttons",
        DockKind.Indicator => "Indicators",
        DockKind.Variable => "Variables",
        DockKind.Blank => "Blank",
        _ => kind.ToString(),
    };



    // public api (names are checked by the config, which knows the other docks)



    internal void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        NameChanged?.Invoke();
        Changed?.Invoke();
    }



    // on / off (not a blank dock): off, nothing changes it until it's back on
    public void SetIsDisabled(bool isDisabled)
    {
        if (!CanDisable || isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        _timers.SetIsOff(isDisabled);
        _sounds.SetIsOff(isDisabled);
        IsDisabledChanged?.Invoke(isDisabled);
        Changed?.Invoke();
    }



    // adds a console line, the oldest go past MaxLines (nothing is saved)
    public void WriteLine(string message)
    {
        if (_isDisabled) return;
        Lines.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        while (Lines.Count > MaxLines) Lines.RemoveAt(0);
    }



    public void ClearLines()
    {
        if (!_isDisabled) Lines.Clear();
    }



    // a button of this dock was pressed: runs its binding (refused if it's not this dock's, unbound or disabled)
    public bool TryPressButton(DockButton button)
    {
        if (_isDisabled || !_buttons.Rows.Contains(button)) return false;
        return button.TryRun();
    }



    // an indicator's state: true = on, false = off, null = nothing has set it since the app started
    public bool? IndicatorState(string name) => _indicatorStates.TryGetValue(name.Trim(), out var isOn) ? isOn : null;



    // sets one of this dock's indicators (a row must have that name, so a typo is an error, not a new indicator):
    // isOn null toggles, and one that was never set turns on. The error is for the block to log
    public bool TrySetIndicator(string name, bool? isOn, out string error)
    {
        error = "";
        if (_isDisabled) return true; // quietly nothing
        var key = name.Trim();
        if (!HasIndicator(key))
        {
            error = $"no indicator '{key}' on dock '{_name}'";
            return false;
        }

        SetIndicator(key, isOn ?? IndicatorState(key) != true);
        return true;
    }



    // every named indicator flipped (an indicator dock's header), one never set turns on
    public void ToggleAllIndicators()
    {
        if (_isDisabled) return;
        foreach (var row in _indicators.Rows.Where(r => !r.IsEmpty).ToList())
            TrySetIndicator(row.Name, null, out _);
    }



    public bool HasIndicator(string name) =>
        name.Trim() != "" && _indicators.Rows.Any(r => string.Equals(r.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));



    private void SetIndicator(string key, bool isOn)
    {
        if (_indicatorStates.TryGetValue(key, out var was) && was == isOn) return;

        _indicatorStates[key] = isOn;
        IndicatorStateChanged?.Invoke(key);
    }



    // every button of this dock, top to bottom (its header's ▶): unbound or disabled ones are skipped
    public void PressAllButtons()
    {
        if (_isDisabled) return;
        foreach (var button in _buttons.Rows.ToList())
            button.TryRun();
    }



    // model callbacks



    private void OnButtonsChanged() => Changed?.Invoke();



    private void OnIndicatorsChanged() => Changed?.Invoke();



    private void OnVariablesChanged() => Changed?.Invoke();



    private void OnSoundsWaitUntilDoneChanged() => Changed?.Invoke();



    // disposal (stops its buttons' runs)



    public void Dispose()
    {
        _buttons.Changed -= OnButtonsChanged;
        _buttons.Dispose();
        _indicators.Changed -= OnIndicatorsChanged;
        _indicators.Dispose();
        _variables.Changed -= OnVariablesChanged;
        _variables.Dispose();
        _timers.Dispose(); // a removed dock's timers end without running their end
        _sounds.WaitUntilDoneChanged -= OnSoundsWaitUntilDoneChanged;
        _sounds.Dispose(); // and its sounds stop
    }
}
