using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Dashboard.Actions;



public class ClearDockAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Clear Dock";
    [JsonIgnore] public override string Description => "Empties a console dock on the Dashboard, found by its name (in the dashboard config in use).";



    // private



    private readonly ValueSlot<string> _dock;
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



    // constructor



    public ClearDockAction()
    {
        _dock = new("Dock");
        _slots = [_dock];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context) =>
        Api(context).Dashboard.ClearDock(await _dock.EvaluateAsync(context));
}
