using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Scripts.Actions;



// runs one of the Scripts tab's scripts (in the config in use) with this run's parameters, plus any set in its rows
// (set top to bottom like Set Parameters, so a later row can use an earlier one's {name}). Waiting until done, the
// run carries on once the script has finished; otherwise straight away. A script that's off, or has no blocks, does
// nothing. Scripts calling scripts are stopped past MaxDepth (one running itself, say)
public class RunScriptAction : ActionBlock
{
    // constants



    private const int MaxDepth = 20;



    // info



    [JsonIgnore] public override string Name => "Run Script";
    [JsonIgnore] public override string Description => "Runs the script named in Script (the Scripts tab, the config in use) with this run's parameters, plus the ones in its rows (a name on each row, its value to the right). Wait Until Done carries on once the script has finished, otherwise straight away. A script that's off does nothing.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Control;



    // private



    private static readonly AsyncLocal<int> Depth = new(); // how many scripts deep this run is

    private readonly MenuSlot _script;
    private bool _waitUntilDone;
    private readonly ValueSlotPairList _rows;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetScriptMenuItems() => GlobalContext.ScriptsComponentModel?.ScriptNames() ?? [];

    [ConfigurableParameter("Wait Until Done", ConfiguratorType.Boolean)] [JsonPropertyName("waitUntilDone")]
    public bool WaitUntilDone
    {
        get => _waitUntilDone;
        set => SetParameter(ref _waitUntilDone, value);
    }

    [JsonIgnore] public ValueSlotPairList Rows => _rows;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("script")]
    private ValueSlotData JsonScript
    {
        get => _script.GetData();
        set => _script.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("rows")]
    private List<ValueSlotPairData> JsonRows
    {
        get => _rows.GetData();
        set => _rows.LoadData(value);
    }



    // constructor



    public RunScriptAction()
    {
        _script = new("Script", GetScriptMenuItems, "", isFixed: false);
        _waitUntilDone = false;
        _rows = new("", "Name…");
        _slots = [_script, _rows];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var name = await _script.EvaluateAsync(context);
        var scripts = GlobalContext.ScriptsComponentModel ?? throw Fail("scripts not set up");
        if (!scripts.TryFindScript(name, out var script, out var error)) throw Fail(error);
        if (Depth.Value >= MaxDepth) throw Fail($"scripts calling scripts went past {MaxDepth} deep (a script running itself?)");

        // this run's parameters, then the rows on top of them
        var call = new TriggerContext(TriggerPhase.Once, context.CancellationToken, context.Parameters);
        foreach (var row in _rows.Items)
        {
            if (row.IsEmpty) continue;

            var key = (await row.Key.EvaluateAsync(call)).Trim();
            if (key == "") throw Fail("a row needs a name");
            if (!TriggerContext.IsValidName(key)) throw Fail($"'{key}' isn't a valid name, use letters, digits, _");

            var (_, value) = await row.Value.EvaluateRawAsync(call);
            call.SetParameter(key, ValueTypes.ToText(value));
        }

        if (script.Bindable is not { } bindable) return;

        // the script's run (and anything it starts) sees one more level
        var depth = Depth.Value;
        Depth.Value = depth + 1;
        try
        {
            if (_waitUntilDone) await bindable.RunOnceAsync(call.Parameters, context.CancellationToken);
            else script.TryRun(call.Parameters);
        }
        finally
        {
            Depth.Value = depth;
        }
    }
}
