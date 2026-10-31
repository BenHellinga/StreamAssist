using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Actions;



public class WaitAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Wait";
    [JsonIgnore] public override string Description => "Waits a number of seconds (decimals work, e.g. 0.5) before the blocks after it run. Removing the binding or closing the app stops the wait.";



    // private



    private readonly ValueSlot<double> _seconds;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }



    // constructor



    public WaitAction()
    {
        _seconds = new("Seconds");
        _slots = [_seconds];

        _seconds.SetText("1");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var seconds = await _seconds.EvaluateAsync(context);
        if (seconds is < 0 or > 86400 || double.IsNaN(seconds)) throw Fail($"{seconds}s, it waits 0 to 86400 (a day)");

        if (seconds > 0) await Task.Delay(TimeSpan.FromSeconds(seconds), context.CancellationToken);
    }
}
