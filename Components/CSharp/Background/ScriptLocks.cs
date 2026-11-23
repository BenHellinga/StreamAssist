using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Utilities;
using StreamAssist.Scripting;
using StreamAssist.Scripting.Runtime;

namespace StreamAssist.Components.CSharp.Background;



// the locks runs take (a table, a variable, a name of their own), owned by the run that took it (its ScriptGuard) and
// shared with the runs it starts. A lock is held until it's disposed or its run ends. Waiting for one gives up after
// the waiting run's Lock wait, which is also what breaks two runs each waiting for the other's lock
//
// Tables and variables: the Data API waits (WaitUntilFree) before every read or change while another run has it
// locked, so a run never sees one half-way through another's change. The UI thread never waits (the Data tab, docks)
public static class ScriptLocks
{
    private sealed class Entry
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public ScriptGuard? Owner;
        public int Depth; // taken again by its own run: let go when each has been
    }



    private static readonly object Changing = new();
    private static readonly Dictionary<string, Entry> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ScriptGuard, List<string>> HeldBy = [];



    // takes the lock for the current run (only a C# script's run can, blocks have no lock block)
    public static async Task<HeldLock> AcquireAsync(string key, string what)
    {
        var guard = ScriptGuard.Current ?? throw new StreamAssistException($"can't lock {what}, only a script can");

        Entry entry;
        lock (Changing)
        {
            entry = EntryOf(key);
            if (entry.Owner != null && guard.IsPartOf(entry.Owner))
            {
                entry.Depth++;
                return new HeldLock(() => Release(key, entry.Owner));
            }
        }

        if (!await WaitAsync(entry.Gate, guard.LockWait, guard.Token)) throw TimedOut(what, guard.LockWait!.Value);

        lock (Changing)
        {
            entry.Owner = guard;
            entry.Depth = 1;
            if (!HeldBy.TryGetValue(guard, out var held)) HeldBy[guard] = held = [];
            held.Add(key);
        }
        return new HeldLock(() => Release(key, guard));
    }



    // waits (blocking the run's thread) while a run this one isn't part of has the lock; the UI thread never waits
    public static void WaitUntilFree(string key, string what, CancellationToken token)
    {
        if (!UiThread.RunsInBackground || Dispatcher.UIThread.CheckAccess()) return;

        var guard = ScriptGuard.Current;
        Entry entry;
        lock (Changing)
        {
            if (!Entries.TryGetValue(key, out var found) || found.Owner == null) return;
            if (guard != null && guard.IsPartOf(found.Owner)) return;
            entry = found;
        }

        // the lock is free once its gate can be taken; it's given straight back, this is only a read or a change
        var wait = guard != null ? guard.LockWait : TimeSpan.FromSeconds(ScriptLimits.Default.LockWaitSeconds!.Value);
        if (!WaitAsync(entry.Gate, wait, token).GetAwaiter().GetResult()) throw TimedOut(what, wait!.Value);
        entry.Gate.Release();
    }



    // lets go of everything a run still holds (when it ends)
    public static void ReleaseAll(ScriptGuard guard)
    {
        List<string>? held;
        lock (Changing)
        {
            if (!HeldBy.Remove(guard, out held)) return;
        }

        foreach (var key in held)
            Release(key, guard, all: true);
    }



    // helpers



    private static void Release(string key, ScriptGuard? owner, bool all = false)
    {
        lock (Changing)
        {
            if (!Entries.TryGetValue(key, out var entry) || entry.Owner != owner || owner == null) return;

            entry.Depth = all ? 0 : entry.Depth - 1;
            if (entry.Depth > 0) return;

            entry.Owner = null;
            if (HeldBy.TryGetValue(owner, out var held)) held.Remove(key);
            entry.Gate.Release();
        }
    }



    private static Entry EntryOf(string key)
    {
        if (!Entries.TryGetValue(key, out var entry)) Entries[key] = entry = new Entry();
        return entry;
    }



    // null: for as long as it takes
    private static async Task<bool> WaitAsync(SemaphoreSlim gate, TimeSpan? wait, CancellationToken token)
    {
        if (wait is { } time) return await gate.WaitAsync(time, token);

        await gate.WaitAsync(token);
        return true;
    }



    private static StreamAssistException TimedOut(string what, TimeSpan waited) =>
        new($"waited {ScriptGuard.Seconds(waited)} for {what}, another run has it locked");
}
