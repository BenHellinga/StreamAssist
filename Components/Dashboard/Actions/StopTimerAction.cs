using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Actions;



public class StopTimerAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Stop Timer";
    [JsonIgnore] public override string Description => "Stops every running timer with that name on the timer dock named in Dock. Finish Now ends it early and runs its When Done blocks, Cancel ends it without them. Nothing happens if none is running.";



    // private



    private readonly MenuSlot _mode;
    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _timerName;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetModeMenuItems() => ["Finish Now", "Cancel"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("mode")]
    private ValueSlotData JsonMode
    {
        get => _mode.GetData();
        set => _mode.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("timer")]
    private ValueSlotData JsonTimerName
    {
        get => _timerName.GetData();
        set => _timerName.LoadData(value);
    }



    // constructor



    public StopTimerAction()
    {
        _mode = new("Mode", GetModeMenuItems, "Finish Now");
        _dock = new("Dock");
        _timerName = new("Timer");
        _slots = [_mode, _dock, _timerName];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var mode = await _mode.EvaluateAsync(context);
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Timer, out var dock, out var dockError)) throw Fail(dockError);
        var timers = dock.Timers;
        var name = await _timerName.EvaluateAsync(context);

        foreach (var timer in timers.Find(name).ToList())
        {
            if (mode == "Cancel") timers.Kill(timer);
            else timers.Finish(timer);
        }
    }
}
