using System.Text.Json;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Scripts.Actions;
using StreamAssist.Components.Scripts.Model;

namespace StreamAssist.Tests;



// engine checks: the Scripts tab: scripts in configs, saving them, and Run Script (parameters passed in, waiting or
// not, a script that's off, scripts calling themselves stopped by the depth guard)
internal static partial class EngineChecks
{
    // ---- scripts: configs of named scripts, Run Script ----
    private static async Task Scripts()
    {
        var old = StreamAssist.Shell.GlobalContext.ScriptsComponentModel;
        var scripts = new ScriptsComponentModel();
        scripts.Configs.TryCreate("Default");
        StreamAssist.Shell.GlobalContext.ScriptsComponentModel = scripts;

        ScriptModel Add(string name, params BlockModel[] blocks)
        {
            var script = scripts.Config!.Scripts[^1]; // naming the empty row adds the next one
            script.SetName(name);
            var bindable = new BindableModel();
            foreach (var block in blocks) bindable.TryAddBlock(block);
            script.TryBind(bindable);
            return script;
        }

        var greet = Add("Greet", new FormatRecordBlock { Template = "{who} {extra}" }, new RecordBlock { Tag = "slow", DelayMs = 150 });
        Check("scripts: naming the last row adds an empty one, names listed (case ignored to find)", scripts.Config!.Scripts.Count == 2
            && scripts.ScriptNames().SequenceEqual(["Greet"]) && scripts.TryFindScript("greet", out var found, out _) && found == greet);

        var ctx = new TriggerContext(TriggerPhase.Once, default, new Dictionary<string, string> { ["who"] = "Bob" });

        RunScriptAction Run(string name, bool wait, params (string Name, string Value)[] rows)
        {
            var run = new RunScriptAction { WaitUntilDone = wait };
            ((ValueSlot)run.Slots[0]).SetText(name);
            foreach (var (key, value) in rows)
            {
                var row = run.Rows.Items[^1];
                row.Key.SetText(key);
                row.Value.SetText(value);
            }
            return run;
        }

        // runs the block then records "after", true if it got there (the block didn't stop the run)
        async Task<bool> Do(BlockModel block)
        {
            RecordBlock.Log.Clear();
            FormatRecordBlock.Log.Clear();
            var list = new ListBlock();
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(block, 0);
            ((BlockSlotList)list.Slots[0]).TryInsertBlock(new RecordBlock { Tag = "after" }, 1);
            await list.SafeRunAsync(ctx);
            return RecordBlock.Log.Contains("after");
        }

        Check("run script: waiting, the script has this run's parameters plus the rows (which can use them), and finishes first",
            await Do(Run("Greet", true, ("extra", "{who}!"))) && FormatRecordBlock.Log.SequenceEqual(["Bob Bob!"])
            && RecordBlock.Log.SequenceEqual(["slow", "late", "after"]));
        Check("run script: the rows don't change the caller's parameters", !ctx.Parameters.ContainsKey("extra"));

        var started = await Do(Run("greet", false));
        Check("run script: not waiting, the run carries on while the script still runs", started && FormatRecordBlock.Log.SequenceEqual(["Bob {extra}"])
            && RecordBlock.Log.SequenceEqual(["slow", "after"]));
        await Task.Delay(400);
        Check("run script: ...and the script still finishes", RecordBlock.Log.Contains("late"));

        Check("run script: a bad row name or an unknown script stops the run", !await Do(Run("Greet", true, ("no spaces", "x"))) && !await Do(Run("Nope", true)));

        greet.Bindable!.SetIsDisabled(true);
        Check("run script: a script that's off does nothing (the run carries on)", await Do(Run("Greet", true)) && FormatRecordBlock.Log.Count == 0);
        greet.Bindable!.SetIsDisabled(false);

        Add("Loop", new RecordBlock { Tag = "loop" }, Run("Loop", true));
        var reached = await Do(Run("Loop", true));
        Check("run script: a script running itself is stopped 20 deep, the caller carries on", reached && RecordBlock.Log.Count(t => t == "loop") == 20);

        var reloaded = JsonSerializer.Deserialize<ScriptsComponentModel>(JsonSerializer.Serialize(scripts))!;
        Check("scripts: saved and loaded with their blocks and the empty row", reloaded.ScriptNames().SequenceEqual(["Greet", "Loop"])
            && reloaded.Config!.Scripts.Count == 3 && reloaded.Config.FindScript("Loop")!.Bindable!.HasBlock);
        var runCopy = (RunScriptAction)BlockResolver.DeepCopy(Run("Greet", true, ("extra", "hi")))!;
        Check("run script: saved and loaded", runCopy.WaitUntilDone && ((ValueSlot)runCopy.Slots[0]).Text == "Greet" && runCopy.Rows.Items[0].Value.Text == "hi");
        reloaded.Dispose();

        scripts.Configs.TryCreate("Other");
        scripts.Configs.TrySelect("Other");
        var copied = scripts.ScriptNames().SequenceEqual(["Greet", "Loop"]);
        scripts.Config!.FindScript("Greet")!.SetName("Hello");
        Check("scripts: a new config copies the scripts, only the config in use's are found", copied && !await Do(Run("Greet", true)) && await Do(Run("Hello", true)));
        scripts.Configs.TrySelect("Default");
        Check("scripts: switching back finds them again", await Do(Run("Greet", true)) && FormatRecordBlock.Log.Count == 1);

        StreamAssist.Shell.GlobalContext.ScriptsComponentModel = old;
        scripts.Dispose();
    }
}
