using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Actions;



public class DockMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Dock Message";
    [JsonIgnore] public override string Description => "Writes a message in a console dock on the Dashboard, found by its name (in the dashboard config in use), not in the app console.";



    // private



    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _message;
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

    [JsonInclude] [JsonPropertyName("message")]
    private ValueSlotData JsonMessage
    {
        get => _message.GetData();
        set => _message.LoadData(value);
    }



    // constructor



    public DockMessageAction()
    {
        _dock = new("Dock");
        _message = new("Message");
        _slots = [_dock, _message];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        var dock = await _dock.EvaluateAsync(context);
        var message = await _message.EvaluateAsync(context);

        if (!dashboard.TryWriteToDock(dock, message, out var error)) throw Fail(error);
    }
}
