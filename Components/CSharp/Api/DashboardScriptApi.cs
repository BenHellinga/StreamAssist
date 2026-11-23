using System;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IDashboard: docks by name in the dashboard config in use (the model refuses unknown ones, with why)
public sealed class DashboardScriptApi(TriggerContext context) : ScriptApiPart(context), IDashboard
{
    private static DashboardComponentModel Dashboard => Need(GlobalContext.DashboardComponentModel, "dashboard");



    // console docks



    public void WriteToDock(string dock, string message) => UiThread.Run(() =>
    {
        if (!Dashboard.TryWriteToDock(dock, message, out var error)) throw Error(error);
    });



    public void ClearDock(string dock) => UiThread.Run(() =>
    {
        if (!Dashboard.TryClearDock(dock, out var error)) throw Error(error);
    });



    // indicators



    public bool IsIndicatorOn(string dock, string indicator) => UiThread.Run<bool>(() =>
    {
        var found = Dock(dock, DockKind.Indicator);
        if (!found.HasIndicator(indicator)) throw Error($"no indicator '{indicator.Trim()}' on dock '{found.Name}'");
        return found.IndicatorState(indicator) == true;
    });



    public void SetIndicator(string dock, string indicator, Switch change) => UiThread.Run(() =>
    {
        bool? isOn = change switch { Switch.On => true, Switch.Off => false, _ => null };
        if (!Dock(dock, DockKind.Indicator).TrySetIndicator(indicator, isOn, out var error)) throw Error(error);
    });



    // timers



    // a script's whenDone runs on a background thread as part of this run; its failure is logged with the timer's name
    public void StartTimer(string? dock, string name, double seconds, Func<Task>? whenDone = null, TimerIfRunning ifRunning = TimerIfRunning.Restart, bool repeat = false) =>
        StartTimer(dock, name, seconds, ifRunning, repeat, whenDone == null ? null : Later($"timer '{name.Trim()}' when done", whenDone), owner: null);



    // for blocks: onEnd runs when it ends (on a background thread), owner's timers can be killed together (a removed
    // Start Timer block's)
    public void StartTimer(string? dock, string name, double seconds, TimerIfRunning ifRunning, bool repeat, Action? onEnd, object? owner)
    {
        onEnd = UiThread.Later(onEnd);
        UiThread.Run(() => StartTimerHere(dock, name, seconds, ifRunning, repeat, onEnd, owner));
    }



    private static void StartTimerHere(string? dock, string name, double seconds, TimerIfRunning ifRunning, bool repeat, Action? onEnd, object? owner)
    {
        var timers = string.IsNullOrWhiteSpace(dock) ? Dashboard.BackgroundTimers : Dock(dock, DockKind.Timer).Timers;
        if (double.IsNaN(seconds) || seconds > 1e9) throw Error($"can't time {seconds} seconds");

        var mode = ifRunning switch
        {
            TimerIfRunning.AddTime => TimerRepeat.AddTime,
            TimerIfRunning.Ignore => TimerRepeat.Ignore,
            TimerIfRunning.RunAnother => TimerRepeat.RunAnother,
            _ => TimerRepeat.Restart,
        };
        if (!timers.TryStart(name, TimeSpan.FromSeconds(seconds), mode, onEnd, owner, out var error, repeat)) throw Error(error);
    }



    public void SetTimersPaused(string dock, string name, Switch change) => UiThread.Run(() =>
    {
        var timers = Dock(dock, DockKind.Timer).Timers;
        foreach (var timer in timers.Find(name).ToList())
            timers.SetIsPaused(timer, change switch { Switch.On => true, Switch.Off => false, _ => !timer.IsPaused });
    });



    public void StopTimers(string dock, string name, bool runWhenDone = true) => UiThread.Run(() =>
    {
        var timers = Dock(dock, DockKind.Timer).Timers;
        foreach (var timer in timers.Find(name).ToList())
        {
            if (runWhenDone) timers.Finish(timer);
            else timers.Kill(timer);
        }
    });



    public double TimerSecondsLeft(string dock, string name) => UiThread.Run<double>(() =>
    {
        var timers = Dock(dock, DockKind.Timer).Timers;
        var now = timers.Now;
        return timers.Find(name).Select(t => t.LeftAt(now)).DefaultIfEmpty(TimeSpan.Zero).Min().TotalSeconds;
    });



    // sounds



    public void SkipSound(string dock) => UiThread.Run(() => Dock(dock, DockKind.Sound).Sounds.StopPlaying());

    public void StopSounds(string dock) => UiThread.Run(() => Dock(dock, DockKind.Sound).Sounds.StopAll());



    // helpers



    private static DockModel Dock(string name, DockKind kind) =>
        Dashboard.TryFindDock(name, kind, out var dock, out var error) ? dock : throw Error(error);
}
