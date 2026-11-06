using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.Dashboard.Model;



// what Start Timer does when a timer with that name is already running
public enum TimerRepeat { Restart, AddTime, Ignore, RunAnother }



// one running (or paused) timer. It keeps when it ends, not a count, so it never drifts; paused, it keeps what was left
public sealed class RunningTimer
{
    public string Name { get; }
    public TimeSpan Total { get; internal set; } // its length, more after Add Time
    public DateTime EndsAt { get; internal set; } // while running
    public TimeSpan? PausedLeft { get; internal set; } // what was left when paused, null while running
    public bool IsPaused => PausedLeft != null;
    public TimeSpan? Lap { get; internal set; } // a repeating timer's length: it starts again each time it runs out
    public bool Repeats => Lap != null;

    internal Action? OnEnd { get; set; } // runs the Start Timer block's end (finished, not killed)
    internal object? Owner { get; set; } // the Start Timer block, its timers go with it



    internal RunningTimer(string name, TimeSpan total, DateTime endsAt, Action? onEnd, object? owner, bool repeats)
    {
        Name = name;
        Total = total;
        EndsAt = endsAt;
        PausedLeft = null;
        Lap = repeats ? total : null;
        OnEnd = onEnd;
        Owner = owner;
    }



    public TimeSpan LeftAt(DateTime now) => PausedLeft ?? (EndsAt > now ? EndsAt - now : TimeSpan.Zero);
}



// one timer dock's timers (DockModel.Timers): found by name (any number with the same name), only kept while the app
// runs. Started by the Start Timer block, ended by the TimerManager (it calls FinishDue on every timer dock's), shown by
// their dock. Finishing runs the timer's end, killing doesn't. A repeating timer that runs out runs its end and starts
// its next lap; finishing it early (or killing it) ends it for good
public sealed class TimersModel : IDisposable
{
    // private



    private readonly List<RunningTimer> _timers;
    private readonly Func<DateTime> _now;



    // public



    public IReadOnlyList<RunningTimer> Timers => _timers;
    public DateTime Now => _now();



    // events



    public event Action? TimersChanged; // one started, ended, was paused or resumed, got more time, or started a lap



    // constructor



    // the clock can be swapped for tests
    public TimersModel(Func<DateTime>? now = null)
    {
        _timers = [];
        _now = now ?? (() => DateTime.UtcNow);
    }



    // public api



    public IEnumerable<RunningTimer> Find(string name) =>
        _timers.Where(t => string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();



    // starts a timer, or (by repeat) restarts / adds time to the ones already running with that name; repeats makes a
    // new or restarted one go round again each time it runs out. The errors are for the block to log
    public bool TryStart(string name, TimeSpan length, TimerRepeat repeat, Action? onEnd, object? owner, out string error, bool repeats = false)
    {
        error = "";
        var trimmed = name.Trim();

        if (trimmed == "")
        {
            error = "timer needs a name";
            return false;
        }

        if (!(length > TimeSpan.Zero))
        {
            error = $"can't time {length.TotalSeconds} seconds";
            return false;
        }

        Start(trimmed, length, repeat, onEnd, owner, repeats);
        return true;
    }



    public void SetIsPaused(RunningTimer timer, bool isPaused)
    {
        if (!_timers.Contains(timer) || timer.IsPaused == isPaused) return;

        var now = _now();
        if (isPaused) timer.PausedLeft = timer.LeftAt(now);
        else
        {
            timer.EndsAt = now + timer.PausedLeft!.Value;
            timer.PausedLeft = null;
        }

        TimersChanged?.Invoke();
    }



    // more (or, negative, less) time left on it; one taken to nothing ends on the next tick
    public void AddTime(RunningTimer timer, TimeSpan amount)
    {
        if (!_timers.Contains(timer) || amount == TimeSpan.Zero) return;

        if (timer.IsPaused) timer.PausedLeft = Max(timer.PausedLeft!.Value + amount, TimeSpan.Zero);
        else timer.EndsAt += amount;
        timer.Total = Max(timer.Total + amount, TimeSpan.Zero);

        TimersChanged?.Invoke();
    }



    // ends it now and runs its end (stop early)
    public void Finish(RunningTimer timer)
    {
        if (!_timers.Remove(timer)) return;

        TimersChanged?.Invoke();
        timer.OnEnd?.Invoke();
    }



    // ends it without running its end
    public void Kill(RunningTimer timer)
    {
        if (!_timers.Remove(timer)) return;
        TimersChanged?.Invoke();
    }



    // every timer at once (a timer dock's header buttons)



    public void SetAllPaused(bool isPaused)
    {
        foreach (var timer in _timers.ToList())
            SetIsPaused(timer, isPaused);
    }



    // the soonest first, like when they run out
    public void FinishAll()
    {
        var now = _now();
        foreach (var timer in _timers.OrderBy(t => t.LeftAt(now)).ToList())
            Finish(timer);
    }



    public void AddTimeAll(TimeSpan amount)
    {
        foreach (var timer in _timers.ToList())
            AddTime(timer, amount);
    }



    public void KillAll()
    {
        if (_timers.Count == 0) return;

        _timers.Clear();
        TimersChanged?.Invoke();
    }



    // the Start Timer block went (its binding was removed): its timers can't run their end any more
    public void KillOwnedBy(object owner)
    {
        if (_timers.RemoveAll(t => t.Owner == owner) > 0) TimersChanged?.Invoke();
    }



    // the TimerManager's tick: every running timer whose time is up finishes, the soonest first; a repeating one runs
    // its end and starts its next lap from when this one ended (so laps don't drift), or from now if it fell behind
    public void FinishDue()
    {
        var now = _now();
        foreach (var timer in _timers.Where(t => !t.IsPaused && t.EndsAt <= now).OrderBy(t => t.EndsAt).ToList())
        {
            if (timer.Lap is not { } lap)
            {
                Finish(timer);
                continue;
            }

            timer.Total = lap;
            timer.EndsAt += lap;
            if (timer.EndsAt <= now) timer.EndsAt = now + lap;

            TimersChanged?.Invoke();
            timer.OnEnd?.Invoke();
        }
    }



    // helpers



    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;



    private void Start(string name, TimeSpan length, TimerRepeat repeat, Action? onEnd, object? owner, bool repeats)
    {
        var now = _now();
        List<RunningTimer> running = repeat == TimerRepeat.RunAnother ? [] : Find(name).ToList();

        if (running.Count == 0)
        {
            _timers.Add(new RunningTimer(name, length, now + length, onEnd, owner, repeats));
            TimersChanged?.Invoke();
            return;
        }

        if (repeat == TimerRepeat.Ignore) return;

        foreach (var timer in running)
        {
            if (repeat == TimerRepeat.Restart)
            {
                // a fresh start: this start's length and end
                timer.Total = length;
                timer.EndsAt = now + length;
                timer.PausedLeft = null;
                timer.OnEnd = onEnd;
                timer.Owner = owner;
                timer.Lap = repeats ? length : null;
            }
            else
            {
                timer.Total += length;
                if (timer.IsPaused) timer.PausedLeft += length;
                else timer.EndsAt += length;
            }
        }

        TimersChanged?.Invoke();
    }



    // disposal (the app is closing: nothing runs its end)



    public void Dispose()
    {
        _timers.Clear();
    }
}
