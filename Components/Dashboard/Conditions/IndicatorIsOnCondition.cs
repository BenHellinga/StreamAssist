using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Conditions;



public class IndicatorIsOnCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Indicator Is On";
    [JsonIgnore] public override string Description => "True while one of an indicator dock's indicators is on (Set Indicator): Dock is the dock's name, Indicator the row's. One that was never set counts as off.";



    // private



    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _indicator;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("indicator")]
    private ValueSlotData JsonIndicator
    {
        get => _indicator.GetData();
        set => _indicator.LoadData(value);
    }



    // constructor



    public IndicatorIsOnCondition()
    {
        _dock = new("Dock");
        _indicator = new("Indicator");
        _slots = [_dock, _indicator];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Indicator, out var dock, out var dockError)) throw Fail(dockError);
        var name = await _indicator.EvaluateAsync(context);
        if (!dock.HasIndicator(name)) throw Fail($"no indicator '{name.Trim()}' on dock '{dock.Name}'");

        return dock.IndicatorState(name) == true;
    }
}
