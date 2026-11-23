using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Background;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Tests;



internal static partial class EngineChecks
{
    // ---- C# scripts: files, compiling, running, errors, copies, sweeping (on the main thread, errors are counted) ----
    private static void CSharpScripts()
    {
        int Errors() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Count(l => l.Contains("] error - ")); }
        string LastError() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(l => l.Contains("] error - ")); }
        string LastLine() { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); return AppLog.Lines.Last(); }

        var folder = Path.Combine(Path.GetTempPath(), "streamassist-scripts-" + Guid.NewGuid().ToString("N"));
        var scripts = new ScriptManager(folder, watch: false);
        var before = BindableModel.Scripts;
        BindableModel.Scripts = scripts;
        try
        {
            Check("scripts: the project file VS Code reads is written, pointing at the API",
                File.ReadAllText(Path.Combine(folder, ScriptManager.ProjectFileName)).Contains("StreamAssist.Scripting.dll") && Directory.Exists(scripts.SharedFolder));

            // switching to C# gives it a script, the template compiles
            var binding = new BindableModel();
            binding.SwitchToCSharp();
            var id = binding.ScriptId!;
            Check("scripts: switching to C# picks an id, which counts as content", binding.IsCSharp && binding.HasContent && id.Length == 12 && char.IsLetter(id[0]));
            Check("scripts: the template is written and compiles", scripts.TryCreateScript(id, "Test binding", held: true)
                && File.ReadAllText(scripts.PathOf(id)).Contains("OnReleasedAsync") && scripts.GetCompiledAsync(id).GetAwaiter().GetResult() != null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Check("scripts: its status says it compiled", scripts.GetStatus(id)?.State == ScriptState.Ready);

            void Write(string body)
            {
                File.WriteAllText(scripts.PathOf(id), $$"""
                    // Test binding (written by the app)
                    namespace Scripts.Test;

                    public sealed class Script : StreamAssistScript
                    {
                    {{body}}
                    }
                    """);
                scripts.Compile(id).GetAwaiter().GetResult();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }
            TriggerContext Run(TriggerPhase phase = TriggerPhase.Once, Dictionary<string, string>? parameters = null)
            {
                var context = new TriggerContext(phase, default, parameters ?? new() { ["user"] = "joe" });
                scripts.RunAsync(id, context, ScriptLimits.Default).GetAwaiter().GetResult();
                return context;
            }

            // parameters, the app console, waits, released
            Write("""
                    public override async Task RunAsync()
                    {
                        Parameters["seen"] = Parameters["user"] + "!";
                        App.Log("hello " + Parameters.Get("missing", "fallback"));
                        await WaitAsync(0.01);
                    }

                    public override Task OnReleasedAsync()
                    {
                        App.Log("let go");
                        return Task.CompletedTask;
                    }
                """);
            var e0 = Errors();
            var ran = Run();
            Check("scripts: reads and sets parameters", ran.Parameters.TryGetValue("seen", out var seen) && seen == "joe!");
            Check("scripts: App.Log prints a line", LastLine().EndsWith("] hello fallback") && Errors() == e0);
            Run(TriggerPhase.Stop);
            Check("scripts: a key's release runs OnReleasedAsync", LastLine().EndsWith("] let go"));

            // errors: logged once with the binding's title and the line; Stop is quiet
            Write("""
                    public override Task RunAsync()
                    {
                        var x = 1;
                        throw new InvalidOperationException("boom");
                    }
                """);
            var e1 = Errors();
            Run();
            Check("scripts: an exception is logged with its title and line", Errors() == e1 + 1 && LastError().EndsWith("error - Test binding: boom (line 9)"));
            Write("""
                    public override Task RunAsync() => Task.FromResult(Parameters["nope"]);
                """);
            Run();
            Check("scripts: a missing parameter is an error", LastError().Contains("Test binding: no parameter 'nope'"));
            Write("""
                    public override async Task RunAsync()
                    {
                        try { await Twitch.SendMessageAsync("hi"); }
                        catch (StreamAssistException) { App.Log("caught"); }
                        await Twitch.SendMessageAsync("again");
                    }
                """);
            var eApi = Errors();
            Run();
            var apiErrors = Errors(); // runs the console's queued lines first
            Check("scripts: an API failure can be caught, uncaught it's logged once under the binding's name",
                AppLog.Lines.Any(l => l.EndsWith("] caught")) && apiErrors == eApi + 1 && LastError().Contains("Test binding: chatbot not set up"));
            Write("""
                    public override Task RunAsync()
                    {
                        Stop();
                        App.Log("after stop");
                        return Task.CompletedTask;
                    }
                """);
            var e2 = Errors();
            Run();
            Check("scripts: Stop ends it quietly", Errors() == e2 && !LastLine().EndsWith("after stop"));

            // a script that doesn't compile: logged when compiled and on every run, never runs an older version
            Write("""
                    public override Task RunAsync() { return Task.CompletedTask }
                """);
            Check("scripts: a compile error is logged with its line", LastError().Contains("Test binding: doesn't compile (line 6:") && scripts.GetStatus(id)?.State == ScriptState.Error);
            var e3 = Errors();
            Run();
            Check("scripts: running one that doesn't compile logs it each time", Errors() == e3 + 1 && LastError().Contains("doesn't compile, fix it first"));
            File.WriteAllText(scripts.PathOf(id), "// Test binding (written by the app)\nnamespace Scripts.Test;\npublic sealed class Other { }\n");
            scripts.Compile(id).GetAwaiter().GetResult();
            Check("scripts: a file without a script class doesn't compile", LastError().Contains("no class that extends StreamAssistScript"));
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var compileLog = scripts.CompileLog(id);
            Check("scripts: the compile output has each compile, every error on its own line",
                compileLog.Any(l => l.EndsWith("] compiled")) && compileLog.Any(l => l.Contains("] error - line 6:")) && compileLog[^1].EndsWith("] error - no class that extends StreamAssistScript"));
            var logChanged = false;
            scripts.CompileLogChanged += changedId => logChanged |= changedId == id;
            scripts.ClearCompileLog(id);
            Check("scripts: clearing the compile output empties it", scripts.CompileLog(id).Count == 0 && logChanged);

            // through its binding: only the mode picked runs
            Write("""
                    public override Task RunAsync() { App.Log("from c#"); return Task.CompletedTask; }
                """);
            var blocks = new RecordBlock { Tag = "blocks" };
            binding.TryAddBlock(blocks);
            RecordBlock.Log.Clear();
            binding.RunOnceAsync(null, default).GetAwaiter().GetResult();
            Check("scripts: in C# mode the script runs, not the blocks", LastLine().EndsWith("] from c#") && RecordBlock.Log.Count == 0);
            binding.SwitchToBlocks();
            binding.RunOnceAsync(null, default).GetAwaiter().GetResult();
            Check("scripts: switched back, the blocks run and the script is kept", RecordBlock.Log.SequenceEqual(new[] { "blocks" }) && binding.ScriptId == id);
            binding.SwitchToCSharp();

            // saved and loaded; a copy gets its own file
            var json = System.Text.Json.JsonSerializer.Serialize(binding);
            Check("scripts: the mode and script are saved", json.Contains("\"mode\":\"csharp\"") && json.Contains($"\"script\":\"{id}\""));
            var copy = BindableModel.DeepCopy(binding);
            Check("scripts: a copy gets its own script file with the same code", copy.ScriptId is { } copyId && copyId != id && copy.IsCSharp
                && File.ReadAllText(scripts.PathOf(copyId)) == File.ReadAllText(scripts.PathOf(id)));
            var blocksOnly = System.Text.Json.JsonSerializer.Serialize(new BindableModel());
            Check("scripts: a blocks binding saves no mode or script", !blocksOnly.Contains("mode") && !blocksOnly.Contains("script"));

            // the template's namespace follows the copy's id
            var fresh = new BindableModel();
            fresh.SwitchToCSharp();
            scripts.TryCreateScript(fresh.ScriptId!, "Fresh", held: false);
            var freshCopy = BindableModel.DeepCopy(fresh);
            Check("scripts: a copied template is renamed into its own namespace",
                !File.ReadAllText(scripts.PathOf(freshCopy.ScriptId!)).Contains(fresh.ScriptId![1..]) && !File.ReadAllText(scripts.PathOf(fresh.ScriptId!)).Contains("OnReleasedAsync"));

            // sweeping: files no binding has go to Removed
            File.WriteAllText(scripts.PathOf("orphanscript1"), "// nobody's");
            copy.Dispose();
            scripts.SweepUnused(BindableModel.LiveScriptIds);
            Check("scripts: unused files are moved to Removed, used ones stay", !File.Exists(scripts.PathOf("orphanscript1"))
                && File.Exists(Path.Combine(scripts.RemovedFolder, "orphanscript1.cs")) && File.Exists(scripts.PathOf(id)));

            binding.Dispose();
            fresh.Dispose();
            freshCopy.Dispose();
        }
        finally
        {
            BindableModel.Scripts = before;
            scripts.Dispose();
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }
}
