using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Actions;



public class SetIndicatorAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Indicator";
    [JsonIgnore] public override string Description => "Turns one of an indicator dock's indicators on (green) or off (red): Dock is the dock's name (in the dashboard config in use), Indicator the row's. Toggle turns one that was never set on. The states are forgotten when the app closes.";



    // private



    private string _set;
    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _indicator;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Set", ConfiguratorType.Menu)] [JsonPropertyName("set")]
    public string Set
    {
        get => _set;
        set => SetParameter(ref _set, value);
    }

    public static List<string> GetSetMenuItems() => ["On", "Off", "Toggle"];

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



    public SetIndicatorAction()
    {
        _set = "On";
        _dock = new("Dock");
        _indicator = new("Indicator");
        _slots = [_dock, _indicator];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Indicator, out var dock, out var dockError)) throw Fail(dockError);
        var name = await _indicator.EvaluateAsync(context);

        bool? isOn = _set switch { "Off" => false, "Toggle" => null, _ => true };
        if (!dock.TrySetIndicator(name, isOn, out var error)) throw Fail(error);
    }
}
