using System;
using System.Threading.Tasks;

namespace StreamAssist.Scripting;



/// <summary>
/// The Dashboard's docks, found by name in the dashboard config in use. An unknown dock, a dock of another kind, or a
/// missing indicator row is an error (a <see cref="StreamAssistException"/>). A turned-off dock quietly takes no changes.
/// Timers, indicators and sounds only last while the app runs.
/// </summary>
public interface IDashboard
{
    // console docks



    /// <summary>Writes a line in a console dock (not the app console).</summary>
    void WriteToDock(string dock, string message);

    /// <summary>Empties a console dock.</summary>
    void ClearDock(string dock);



    // indicator docks



    /// <summary>Whether one of an indicator dock's indicators is on (one never set counts as off).</summary>
    bool IsIndicatorOn(string dock, string indicator);

    /// <summary>Turns an indicator on (green), off (red) or toggles it (one never set turns on).</summary>
    void SetIndicator(string dock, string indicator, Switch change);



    // timers



    /// <summary>
    /// Starts a timer and carries on straight away. With a dock it shows on that timer dock (where it can be paused or
    /// stopped); with none it runs in the background. <paramref name="whenDone"/> runs when it runs out (or is finished
    /// early); <paramref name="repeat"/> starts it again each time it ends.
    /// </summary>
    void StartTimer(string? dock, string name, double seconds, Func<Task>? whenDone = null, TimerIfRunning ifRunning = TimerIfRunning.Restart, bool repeat = false);

    /// <summary>Pauses (On), resumes (Off) or toggles every running timer with this name on a timer dock.</summary>
    void SetTimersPaused(string dock, string name, Switch change);

    /// <summary>Stops every running timer with this name: finished now (their whenDone runs) or cancelled (it doesn't).</summary>
    void StopTimers(string dock, string name, bool runWhenDone = true);

    /// <summary>Seconds a timer has left (the soonest if several share the name), 0 if none is running.</summary>
    double TimerSecondsLeft(string dock, string name);



    // sound docks



    /// <summary>Skips the sound playing on a sound dock (the next one starts).</summary>
    void SkipSound(string dock);

    /// <summary>Stops the sound playing on a sound dock and every one waiting.</summary>
    void StopSounds(string dock);
}



/// <summary>What starting a timer does when one with that name is already running.</summary>
public enum TimerIfRunning
{
    /// <summary>Starts it over with the new time.</summary>
    Restart,

    /// <summary>Adds the time to the one running.</summary>
    AddTime,

    /// <summary>Leaves the running one as it is.</summary>
    Ignore,

    /// <summary>Starts another one alongside it.</summary>
    RunAnother,
}
