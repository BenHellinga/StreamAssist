using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Actions;



public class StopSoundsAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Stop Sounds";
    [JsonIgnore] public override string Description => "Stops sounds on the sound dock named in Dock: Playing skips the one playing (the next one starts), All stops it and every one waiting. Sounds played with no dock can't be stopped. Nothing happens if none are there.";



    // private



    private readonly MenuSlot _which;
    private readonly ValueSlot<string> _dock;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetWhichMenuItems() => ["All", "Playing"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("which")]
    private ValueSlotData JsonWhich
    {
        get => _which.GetData();
        set => _which.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }



    // constructor



    public StopSoundsAction()
    {
        _which = new("Which", GetWhichMenuItems, "All");
        _dock = new("Dock");
        _slots = [_which, _dock];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var which = await _which.EvaluateAsync(context);
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Sound, out var dock, out var dockError)) throw Fail(dockError);

        if (which == "Playing") dock.Sounds.StopPlaying();
        else dock.Sounds.StopAll();
    }
}
