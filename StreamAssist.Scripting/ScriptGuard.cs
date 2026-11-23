using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace StreamAssist.Scripting.Runtime;



/// <summary>
/// Keeps a run's script in check; scripts never use it. When a script is compiled the app adds a call to
/// <see cref="Loop"/> at the top of every loop and to <see cref="Call"/> at the start of every method (on the same
/// line, so line numbers don't change). They end the run when it's cancelled, when a loop has gone on for longer than
/// the binding's loop limit without waiting for anything, and before recursion that never ends overflows the stack
/// (which would close the whole app).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ScriptGuard
{
    // the run's guard flows with its code (into awaits, tasks it starts, callbacks it set up), so each check knows its run.
    // Its code resuming on a thread (after an await that really waited) starts a new busy stretch on that thread
    private static readonly AsyncLocal<ScriptGuard?> CurrentGuard = new(OnCurrentChanged);

    [ThreadStatic] private static ScriptGuard? _busyGuard;
    [ThreadStatic] private static long _busySince;
    [ThreadStatic] private static int _passes;

    private volatile string? _stoppedBecause;



    /// <summary>The guard of the run whose code is running here, null outside a script.</summary>
    public static ScriptGuard? Current => CurrentGuard.Value;

    /// <summary>The run that started this one (Run Script, a callback it set up), null for a trigger's own run.</summary>
    public ScriptGuard? Parent { get; }

    /// <summary>The binding's name, for messages.</summary>
    public string Title { get; }

    /// <summary>Cancelled when the run should stop (including its run limit).</summary>
    public CancellationToken Token { get; }

    /// <summary>How long a loop can go on without waiting (null: no limit).</summary>
    public TimeSpan? LoopLimit { get; }

    /// <summary>How long it waits for a locked table, variable or lock before giving up (null: for as long as it takes).</summary>
    public TimeSpan? LockWait { get; }

    /// <summary>Why a limit stopped the run, null while it hasn't.</summary>
    public string? StoppedBecause => _stoppedBecause;



    public ScriptGuard(ScriptGuard? parent, string title, CancellationToken token, TimeSpan? loopLimit, TimeSpan? lockWait)
    {
        Parent = parent;
        Title = title;
        Token = token;
        LoopLimit = loopLimit;
        LockWait = lockWait;
    }



    /// <summary>Makes this the guard of the code that runs from here on in the calling async method (and what it calls).</summary>
    public void Use() => CurrentGuard.Value = this;



    /// <summary>Whether this run is <paramref name="other"/> or was started by it (so it shares what that run holds).</summary>
    public bool IsPartOf(ScriptGuard other)
    {
        for (var guard = this; guard != null; guard = guard.Parent)
            if (guard == other) return true;
        return false;
    }



    // the checks the compiler adds



    /// <summary>Added at the top of every loop.</summary>
    public static void Loop()
    {
        var guard = CurrentGuard.Value;
        if (guard != null) guard.Check();
    }



    /// <summary>Added at the start of every method: recursion is stopped before it overflows the stack.</summary>
    public static void Call()
    {
        var guard = CurrentGuard.Value;
        if (guard == null) return;

        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack()) guard.StopBecause("recursion too deep (a method calling itself forever?)");
        guard.Check();
    }



    // a stopped run stays stopped: a catch inside a loop only gets as far as the next check
    private void Check()
    {
        if (_stoppedBecause != null) throw new ScriptLimitException(_stoppedBecause);
        Token.ThrowIfCancellationRequested();

        if (LoopLimit is not { } limit) return;
        if (_busyGuard != this)
        {
            _busyGuard = this;
            _busySince = Stopwatch.GetTimestamp();
            return;
        }

        // the clock is read every so often, a pass is usually far quicker than reading it
        if ((++_passes & 63) != 0) return;
        if (Stopwatch.GetElapsedTime(_busySince) > limit) StopBecause($"looped {Seconds(limit)} without waiting (loop limit)");
    }



    /// <summary>Ends the run with this reason (logged once by whoever ran it).</summary>
    public void StopBecause(string reason)
    {
        _stoppedBecause ??= reason;
        throw new ScriptLimitException(_stoppedBecause);
    }



    // the run's code resumed on this thread (or left it): its busy stretch starts again
    private static void OnCurrentChanged(AsyncLocalValueChangedArgs<ScriptGuard?> change)
    {
        if (!change.ThreadContextChanged) return;
        _busyGuard = change.CurrentValue;
        _busySince = Stopwatch.GetTimestamp();
    }



    /// <summary>e.g. "5 s", "0.5 s".</summary>
    public static string Seconds(TimeSpan time) => $"{time.TotalSeconds:0.###} s";
}



/// <summary>Thrown when a limit (loop limit, run limit, recursion) stops a run; the app logs why.</summary>
public sealed class ScriptLimitException(string reason) : Exception(reason);
