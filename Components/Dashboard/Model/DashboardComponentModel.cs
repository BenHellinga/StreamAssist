using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Dashboard.Model;



// the Dashboard tab: what a streamer watches during the stream, as docks laid out in regions (see
// DashboardConfigModel), one config in use at a time. Blocks find a dock by name in the config in use (TryFindDock);
// timers and indicator states live on their dock, only while the app runs
public sealed class DashboardComponentModel : IDisposable
{
    // private



    private readonly ConfigSet<DashboardConfigModel> _configs;
    private readonly TimersModel _backgroundTimers; // timers started with no dock: nothing shows or changes them, they just end



    // public



    [JsonIgnore] public ConfigSet<DashboardConfigModel> Configs => _configs;
    [JsonIgnore] public DashboardConfigModel? Config => _configs.Selected;
    [JsonIgnore] public TimersModel BackgroundTimers => _backgroundTimers;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<DashboardConfigModel> JsonConfigs
    {
        get => _configs.Items.ToList();
        set => _configs.LoadItems(value);
    }

    [JsonInclude] [JsonPropertyName("selectedConfig")]
    private string? JsonSelectedConfig
    {
        get => _configs.SelectedName;
        set => _configs.LoadSelected(value);
    }



    // events



    public event Action? Changed; // anything saved changed



    // constructor



    public DashboardComponentModel()
    {
        _configs = new("dashboard config", () => new DashboardConfigModel());
        _backgroundTimers = new();

        _configs.Changed += OnConfigsChanged;
    }



    // public api



    // a dock of the config in use by name, which must be of this kind (the error is for the block to log, it starts it
    // with its own name)
    public bool TryFindDock(string name, DockKind kind, out DockModel dock, out string error)
    {
        dock = null!;
        error = "";

        if (Config == null)
        {
            error = "no dashboard config";
            return false;
        }

        if (Config.FindDock(name) is not { } found)
        {
            error = $"no dock '{name.Trim()}'";
            return false;
        }

        if (found.Kind != kind)
        {
            error = $"dock '{found.Name}' isn't {KindWithArticle(kind)}";
            return false;
        }

        dock = found;
        return true;
    }



    // where a sound goes (Play Sound, Speak Text): a sound dock's queue, or null for no dock (an empty name: the sound
    // plays straight away over anything else, and can't be stopped)
    public bool TryFindSoundQueue(string name, out SoundsModel? sounds, out string error)
    {
        sounds = null;
        error = "";
        if (name.Trim() == "") return true;
        if (!TryFindDock(name, DockKind.Sound, out var dock, out error)) return false;

        sounds = dock.Sounds;
        return true;
    }



    // a console dock of the config in use, by name (Dock Message)
    public bool TryWriteToDock(string name, string message, out string error)
    {
        if (!TryFindDock(name, DockKind.Console, out var dock, out error)) return false;

        dock.WriteLine(message);
        return true;
    }



    public bool TryClearDock(string name, out string error)
    {
        if (!TryFindDock(name, DockKind.Console, out var dock, out error)) return false;

        dock.ClearLines();
        return true;
    }



    // every timer dock's timers, in every config (a config not in use keeps its timers running), and the ones with no dock
    public List<TimersModel> AllTimers() =>
        [.. _configs.Items.SelectMany(c => c.Docks).Where(d => d.Kind == DockKind.Timer).Select(d => d.Timers), _backgroundTimers];



    // the TimerManager's tick
    public void FinishDueTimers()
    {
        foreach (var timers in AllTimers())
            timers.FinishDue();
    }



    // a Start Timer block went: wherever its timers are, nothing is left to run when they end
    public void KillTimersOwnedBy(object owner)
    {
        foreach (var timers in AllTimers())
            timers.KillOwnedBy(owner);
    }



    // helpers



    private static string KindWithArticle(DockKind kind) => kind switch
    {
        DockKind.Console => "a console",
        DockKind.Timer => "a timer dock",
        DockKind.Sound => "a sound dock",
        DockKind.Button => "a button dock",
        DockKind.Indicator => "an indicator dock",
        _ => "a variable dock",
    };



    private void OnConfigsChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        _configs.Changed -= OnConfigsChanged;
        _configs.Dispose();
        _backgroundTimers.Dispose();
    }
}
