using System.Text.Json;
using Avalonia.Threading;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Background;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Utilities;
using StreamAssist.Shell;

namespace StreamAssist.Tests;



internal static partial class EngineChecks
{
    // ---- runs on background threads: the script guard (loop limit, run limit, recursion, cancellation), the UI thread
    // doing the app's work, locks, saved limits (on the main thread, which does the UI queue's work itself while it waits) ----
    private static void BackgroundRuns()
    {
        int Errors() { Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        bool Logged(string text) { Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Any(l => l.EndsWith("] " + text)); }

        // waits for runs on other threads while doing the UI thread's work they hand it (what the app's loop does)
        bool Pump(Task task, double seconds = 10)
        {
            var until = DateTime.Now.AddSeconds(seconds);
            while (!task.IsCompleted && DateTime.Now < until)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(1);
            }
            Dispatcher.UIThread.RunJobs();
            return task.IsCompleted;
        }

        var folder = Path.Combine(Path.GetTempPath(), "streamassist-threads-" + Guid.NewGuid().ToString("N"));
        var scripts = new ScriptManager(folder, watch: false);
        var beforeScripts = BindableModel.Scripts;
        var beforeData = GlobalContext.DataModel;
        BindableModel.Scripts = scripts;
        var testThread = Environment.CurrentManagedThreadId;
        var bindings = new List<BindableModel>();
        DataStore? dataStore = null;

        // a script with this body, compiled (its body starts on line 5)
        void Write(string id, string body)
        {
            File.WriteAllText(scripts.PathOf(id), $$"""
                // Guarded (written by the app)
                namespace Scripts.Guarded;
                public sealed class Script : StreamAssistScript
                {
                {{body}}
                }
                """);
            scripts.Compile(id).GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs();
        }

        void Run(string id, ScriptLimits limits, CancellationToken token = default) =>
            scripts.RunAsync(id, new TriggerContext(TriggerPhase.Once, token), limits).GetAwaiter().GetResult();

        BindableModel Binding(string body, ScriptLimits? limits = null)
        {
            var binding = new BindableModel();
            binding.SwitchToCSharp();
            if (limits != null) binding.TrySetScriptLimits(limits);
            Write(binding.ScriptId!, body);
            bindings.Add(binding);
            return binding;
        }

        try
        {
            // ---- the guard, on this thread (runs started in place, as everywhere else in the tests) ----

            const string id = "guardedscript";
            var quick = new ScriptLimits(0.2, null, 10);

            Write(id, """
                    public override Task RunAsync()
                    {
                        while (true) { }
                    }
                """);
            var e0 = Errors();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Run(id, quick);
            Check("guard: a loop that never waits is stopped at its loop limit, with its line",
                Errors() == e0 + 1 && LastError().EndsWith("error - Guarded: stopped, looped 0.2 s without waiting (loop limit) (line 7)") && clock.Elapsed < TimeSpan.FromSeconds(3));

            Write(id, """
                    public override Task RunAsync()
                    {
                        while (true)
                        {
                            try { while (true) { } }
                            catch (Exception) { }
                        }
                    }
                """);
            var e1 = Errors();
            Run(id, quick);
            Check("guard: catching the stop inside a loop doesn't keep it going", Errors() == e1 + 1 && LastError().Contains("Guarded: stopped, looped 0.2 s"));

            Write(id, """
                    public override Task RunAsync() => Task.FromResult(Down(0));
                    static int Down(int n) => Down(n + 1) + 1;
                """);
            Run(id, quick);
            Check("guard: recursion that never ends is stopped before it closes the app", LastError().Contains("Guarded: stopped, recursion too deep"));

            Write(id, """
                    public override async Task RunAsync()
                    {
                        for (var i = 0; i < 12; i++) await WaitAsync(0.03);
                        App.Log("waited all the way");
                    }
                """);
            var e2 = Errors();
            Run(id, quick);
            Check("guard: a loop that waits each time round goes on past the loop limit", Errors() == e2 && Logged("waited all the way"));

            Write(id, """
                    public override async Task RunAsync() => await WaitAsync(30);
                """);
            clock.Restart();
            Run(id, new ScriptLimits(null, 0.2, 10));
            Check("guard: a run is stopped at its run limit", LastError().EndsWith("error - Guarded: stopped after 0.2 s (run limit)") && clock.Elapsed < TimeSpan.FromSeconds(3));

            Write(id, """
                    public override Task RunAsync()
                    {
                        while (true) { }
                    }
                """);
            var e3 = Errors();
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(0.2)))
            {
                var cancelled = false;
                try { Run(id, new ScriptLimits(null, null, 10), cancel.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                Check("guard: with no loop limit, a loop is still stopped quietly when its run is cancelled", cancelled && Errors() == e3);
            }

            // expression bodies become block bodies, everything still works and lines stay the same
            Write(id, """
                    int Five => 5;
                    static int Twice(int n) => n * 2;
                    int this[int i] => i + 1;
                    static void Fail() => throw new InvalidOperationException("nope");
                    public override async Task RunAsync()
                    {
                        int Local(int x) => x * 3;
                        Func<int, int> lambda = x => { return x + 1; };
                        var i = 0;
                        again:
                        i++;
                        if (i < 3) goto again;
                        var sum = 0;
                        foreach (var n in new[] { 1, 2 }) sum += n;
                        do { sum++; } while (false);
                        for (var k = 0; k < 2; k++) sum++;
                        App.Log($"shapes {Five} {Twice(2)} {this[1]} {Local(2)} {lambda(1)} {i} {sum}");
                        await Task.CompletedTask;
                        Fail();
                    }
                """);
            Run(id, quick);
            Check("guard: properties, indexers, local functions, lambdas, goto and every loop still work", Logged("shapes 5 4 2 6 2 3 6"));
            Check("guard: a throw in an expression body keeps its line", LastError().EndsWith("error - Guarded: nope (line 8)"));

            // ---- runs on background threads, this thread doing the UI's work ----

            UiThread.RunsInBackground = true;

            var stuck = Binding("""
                    public override Task RunAsync()
                    {
                        App.Log("thread " + Environment.CurrentManagedThreadId);
                        while (true) { }
                    }
                """, new ScriptLimits(0.5, null, 10));
            clock.Restart();
            var stuckRun = stuck.RunOnceAsync(null, default);
            var returnedAt = clock.Elapsed;
            Check("background: a stuck script doesn't hold up the thread that started it", returnedAt < TimeSpan.FromSeconds(0.3) && !stuckRun.IsCompleted);
            Check("background: it ends at its loop limit", Pump(stuckRun) && LastError().Contains("stopped, looped 0.5 s without waiting"));
            Check("background: it ran on another thread", AppLog.Lines.Any(l => l.Contains("] thread ") && !l.EndsWith($"] thread {testThread}")));

            // a quick press and release run in that order, though each is on its own thread
            var held = Binding("""
                    public override Task RunAsync() { App.Log("tap down"); return Task.CompletedTask; }
                    public override Task OnReleasedAsync() { App.Log("tap up"); return Task.CompletedTask; }
                """);
            for (var i = 0; i < 20; i++)
            {
                held.TriggerStart();
                held.TriggerStop();
            }
            Pump(Task.Delay(300));
            var taps = AppLog.Lines.Where(l => l.EndsWith("] tap down") || l.EndsWith("] tap up")).Select(l => l.EndsWith("down")).ToList();
            Check("background: a binding's runs start in the order they were triggered (press before release)",
                taps.Count == 40 && Enumerable.Range(0, 40).All(i => taps[i] == (i % 2 == 0)));

            // the app's work (here a variable) is done on the UI thread
            var data = new DataModel();
            dataStore = new DataStore(data, Path.Combine(folder, "data.db")); // makes the variables table
            GlobalContext.DataModel = data;
            data.TryAddRow("variables", out var countRow);
            data.TrySetValue("variables", countRow.Id, "name", "count");
            data.TrySetValue("variables", countRow.Id, "type", "Int");
            var changedOn = new List<int>();
            data.ValueChanged += (_, _, _) => changedOn.Add(Environment.CurrentManagedThreadId);
            int Count() => data.TryGetVariable("count", out _, out var value) ? (int)value : -1;

            var change = Binding("""
                    public override Task RunAsync()
                    {
                        Data.ChangeVariable("count", 1);
                        return Task.CompletedTask;
                    }
                """);
            Check("background: what a script asks the app to do is done on the UI thread",
                Pump(change.RunOnceAsync(null, default)) && Count() == 1 && changedOn.Count > 0 && changedOn.All(t => t == testThread));

            // a block flashing in the UI from a run on another thread
            var flashedOn = new List<int>();
            var record = new RecordBlock { Tag = "flash" };
            record.Triggered += () => flashedOn.Add(Environment.CurrentManagedThreadId);
            var blocks = new BindableModel();
            bindings.Add(blocks);
            blocks.TryAddBlock(record);
            Check("background: a block run on another thread flashes on the UI thread", Pump(blocks.RunOnceAsync(null, default)) && flashedOn.SequenceEqual(new[] { testThread }));

            // locks: five runs at once each read, wait and write back: with the lock none of them is lost
            var counter = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await Data.LockVariableAsync("count"))
                        {
                            var now = Data.GetVariable<int>("count");
                            await WaitAsync(0.05);
                            Data.SetVariable("count", now + 1);
                        }
                    }
                """);
            var startCount = Count();
            var runs = Enumerable.Range(0, 5).Select(_ => counter.RunOnceAsync(null, default)).ToArray();
            Check("locks: runs at once that lock a variable each see the one before's change", Pump(Task.WhenAll(runs)) && Count() == startCount + 5);

            // a run that doesn't lock still waits for one that has it locked before reading
            var holder = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await Data.LockVariableAsync("count"))
                        {
                            await WaitAsync(0.3);
                            Data.SetVariable("count", 100);
                        }
                    }
                """);
            var reader = Binding("""
                    public override Task RunAsync()
                    {
                        App.Log("read " + Data.GetVariable<int>("count"));
                        return Task.CompletedTask;
                    }
                """);
            var holding = holder.RunOnceAsync(null, default);
            Pump(Task.Delay(50));
            Check("locks: reading a locked variable waits until it's let go", Pump(Task.WhenAll(holding, reader.RunOnceAsync(null, default))) && Logged("read 100"));

            // waiting too long fails with why
            var longHolder = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await Data.LockVariableAsync("count")) await WaitAsync(1);
                    }
                """);
            var impatient = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await Data.LockVariableAsync("count")) App.Log("never");
                    }
                """, new ScriptLimits(5, null, 0.2));
            var longHolding = longHolder.RunOnceAsync(null, default);
            Pump(Task.Delay(50));
            Pump(impatient.RunOnceAsync(null, default));
            Check("locks: waiting longer than the lock wait fails, saying why", LastError().Contains("waited 0.2 s for variable count, another run has it locked") && !Logged("never"));
            Pump(longHolding);

            // a lock still held when its run ends is let go
            var forgetful = Binding("""
                    public override async Task RunAsync() => await Data.LockVariableAsync("count");
                """);
            var after = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await Data.LockVariableAsync("count")) App.Log("got it");
                    }
                """, new ScriptLimits(5, null, 0.2));
            Pump(forgetful.RunOnceAsync(null, default));
            Check("locks: a lock its run didn't let go of is let go when the run ends", Pump(after.RunOnceAsync(null, default)) && Logged("got it"));

            // named locks of a script's own
            var named = Binding("""
                    public override async Task RunAsync()
                    {
                        using (await App.LockAsync("mine"))
                        using (await App.LockAsync("MINE")) App.Log("twice");
                    }
                """);
            Check("locks: a run can take its own lock again (names ignore case)", Pump(named.RunOnceAsync(null, default)) && Logged("twice"));

            UiThread.RunsInBackground = false;

            // ---- limits are kept with the binding ----

            var limited = new BindableModel();
            bindings.Add(limited);
            Check("limits: a new binding has the default ones, not saved", limited.ScriptLimits == ScriptLimits.Default && !JsonSerializer.Serialize(limited).Contains("limits"));
            var e4 = Errors();
            Check("limits: a negative one is refused", !limited.TrySetScriptLimits(new(-1, null, 10)) && Errors() == e4 + 1 && limited.ScriptLimits == ScriptLimits.Default);
            limited.TrySetScriptLimits(new(30, null, 2.5));
            var saved = JsonSerializer.Serialize(limited);
            var loaded = JsonSerializer.Deserialize<BindableModel>(saved)!;
            bindings.Add(loaded);
            Check("limits: changed ones are saved (0 for off) and loaded", saved.Contains("\"limits\":{\"loop\":30,\"run\":0,\"lockWait\":2.5}") && loaded.ScriptLimits == new ScriptLimits(30, null, 2.5));

            // a list read while it's changed keeps what it had
            var list = new SnapshotList<int>();
            list.Add(1);
            list.Add(2);
            foreach (var item in list) list.Add(item * 10);
            Check("snapshot list: changing it while looping over it is safe", list.SequenceEqual(new[] { 1, 2, 10, 20 }));
        }
        finally
        {
            UiThread.RunsInBackground = false;
            foreach (var binding in bindings) binding.Dispose();
            dataStore?.Dispose();
            BindableModel.Scripts = beforeScripts;
            GlobalContext.DataModel = beforeData;
            scripts.Dispose();
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
