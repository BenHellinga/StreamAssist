using StreamAssist.Scripting;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

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
        var dock = await _dock.EvaluateAsync(context);
        var name = await _timerName.EvaluateAsync(context);
        Api(context).Dashboard.SetTimersPaused(dock, name, setType switch { "Pause" => Switch.On, "Resume" => Switch.Off, _ => Switch.Toggle });
    }
}
