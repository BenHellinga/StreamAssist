using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.CSharp.Background;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IApp: the console, configs, and the Scripts tab
public sealed class AppScriptApi(TriggerContext context) : ScriptApiPart(context), IApp
{
    // scripts running scripts (Run Script, or a script's RunScriptAsync) stop this deep: one running itself would never end
    public const int MaxScriptDepth = 20;
    private static readonly AsyncLocal<int> Depth = new();



    public void Log(string message) => AppLog.WriteLine(message); // safe from any thread



    public void SetConfig(ConfigTab tab, string name) => UiThread.Run(() =>
    {
        var configs = ConfigsOf(tab) ?? throw Error($"no {tab} tab");
        var wanted = name.Trim();
        if (!configs.Names.Any(n => string.Equals(n, wanted, StringComparison.OrdinalIgnoreCase))) throw Error($"no {tab} config '{wanted}'");

        configs.TrySelect(wanted);
    });



    public string? GetConfig(ConfigTab tab) => UiThread.Run<string?>(() => ConfigsOf(tab)?.SelectedName);



    // found on the UI thread, run from this run's thread: what it starts belongs to this run (AsyncLocal flows into it:
    // the depth below, its locks, its limits)
    public async Task<bool> RunScriptAsync(string name, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var bindable = UiThread.Run(() => FindScript(name).Bindable);
        if (bindable == null) return false;

        var depth = Deeper();
        try
        {
            return await bindable.RunOnceAsync(Merged(parameters), Token);
        }
        finally
        {
            Depth.Value = depth;
        }
    }



    public bool StartScript(string name, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var script = UiThread.Run(() => FindScript(name));

        // what it starts sees one more level too (AsyncLocal flows into the run it starts)
        var depth = Deeper();
        try
        {
            return script.TryRun(Merged(parameters));
        }
        finally
        {
            Depth.Value = depth;
        }
    }



    // a lock of the script's own (only runs asking for the same name wait)
    public Task<HeldLock> LockAsync(string name)
    {
        var key = name.Trim();
        if (key == "") throw Error("a lock needs a name");
        return ScriptLocks.AcquireAsync("lock:" + key.ToLowerInvariant(), $"lock '{key}'");
    }



    // helpers



    // the Scripts tab's script (its Bindable null: nothing to run)
    private static StreamAssist.Components.Scripts.Model.ScriptModel FindScript(string name)
    {
        var scripts = Need(GlobalContext.ScriptsComponentModel, "scripts");
        return scripts.TryFindScript(name, out var script, out var error) ? script : throw Error(error);
    }



    // one level deeper for what this run starts, refusing past MaxScriptDepth; gives the depth to put back
    private static int Deeper()
    {
        var depth = Depth.Value;
        if (depth >= MaxScriptDepth) throw Error($"scripts calling scripts went past {MaxScriptDepth} deep (a script running itself?)");
        Depth.Value = depth + 1;
        return depth;
    }



    // this run's parameters with the given ones on top
    private Dictionary<string, string> Merged(IReadOnlyDictionary<string, string>? parameters)
    {
        var merged = new Dictionary<string, string>(Context.Parameters);
        foreach (var (key, value) in parameters ?? new Dictionary<string, string>())
        {
            if (!TriggerContext.IsValidName(key)) throw Error($"'{key}' isn't a valid name, use letters, digits, _");
            merged[key] = value;
        }
        return merged;
    }



    // the tab's configs (null if the app hasn't made that tab's model, e.g. in the tests)
    private static IConfigSet? ConfigsOf(ConfigTab tab) => tab switch
    {
        ConfigTab.Input => GlobalContext.InputComponentModel?.ListModel.Configs,
        ConfigTab.Chat => GlobalContext.ChatbotComponentModel?.ChatConfigs,
        ConfigTab.Events => GlobalContext.ChatbotComponentModel?.EventsConfigs,
        ConfigTab.Dashboard => GlobalContext.DashboardComponentModel?.Configs,
        ConfigTab.Scripts => GlobalContext.ScriptsComponentModel?.Configs,
        ConfigTab.Themes => GlobalContext.ThemeComponentModel?.Configs,
        _ => null,
    };
}
