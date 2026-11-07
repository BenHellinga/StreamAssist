using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.Actions;



public class PauseTimerAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Pause Timer";
    [JsonIgnore] public override string Description => "Pauses or resumes every running timer with that name on the timer dock named in Dock (a paused timer keeps what it had left). Nothing happens if none is running.";



    // private



    private readonly MenuSlot _setType;
    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _timerName;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetSetTypeMenuItems() => ["Pause", "Resume", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("setType")]
    private ValueSlotData JsonSetType
    {
        get => _setType.GetData();
        set => _setType.LoadData(value);
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



    public PauseTimerAction()
    {
        _setType = new("Set Type", GetSetTypeMenuItems, "Toggle");
        _dock = new("Dock");
        _timerName = new("Timer");
        _slots = [_setType, _dock, _timerName];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var setType = await _setType.EvaluateAsync(context);
        var dashboard = GlobalContext.DashboardComponentModel ?? throw Fail("no dashboard");
        if (!dashboard.TryFindDock(await _dock.EvaluateAsync(context), DockKind.Timer, out var dock, out var dockError)) throw Fail(dockError);
        var timers = dock.Timers;
        var name = await _timerName.EvaluateAsync(context);

        foreach (var timer in timers.Find(name).ToList())
            timers.SetIsPaused(timer, setType switch { "Pause" => true, "Resume" => false, _ => !timer.IsPaused });
    }
}
