using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Dashboard.Values;



public class TimerLeftBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Timer Left";
    [JsonIgnore] public override string Description => "How many seconds a timer on the timer dock named in Dock has left (rounded up), the soonest one if several share the name. 0 if none is running, so Timer Left > 0 says whether it is.";



    // private



    private readonly ValueSlot<string> _dock;
    private readonly ValueSlot<string> _timerName;
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

    [JsonInclude] [JsonPropertyName("timer")]
    private ValueSlotData JsonTimerName
    {
        get => _timerName.GetData();
        set => _timerName.LoadData(value);
    }



    // constructor



    public TimerLeftBlock()
    {
        _dock = new("Dock");
        _timerName = new("Timer");
        _slots = [_dock, _timerName];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context)
    {
        var dock = await _dock.EvaluateAsync(context);
        var name = await _timerName.EvaluateAsync(context);
        return (int)Math.Ceiling(Api(context).Dashboard.TimerSecondsLeft(dock, name));
    }
}
