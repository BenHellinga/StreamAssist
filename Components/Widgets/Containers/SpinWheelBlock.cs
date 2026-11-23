using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Containers;



public class SpinWheelBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Spin Wheel";
    [JsonIgnore] public override string Description => "Spins the wheel widget named in Widget (picking its winner by weight now) and carries on straight away. Once it has stopped and shown its winner for its Winner Time, the blocks in When Done run with this run's parameters as they were at the start, plus {winner}. Still spinning or showing its last winner, or turned off: nothing happens.";
    [JsonIgnore] public override string? Subgroup => "Wheel";



    // private



    private readonly MenuSlot _widget;
    private readonly BlockSlot _whenDone;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("whenDone")]
    private PendingBlockData? JsonWhenDone
    {
        get => _whenDone.GetData();
        set => _whenDone.LoadData(value);
    }



    // constructor



    public SpinWheelBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _whenDone = new("When Done", typeof(ExecutableBlock));
        _slots = [_widget, _whenDone];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var widget = await _widget.EvaluateAsync(context);
        var endContext = new TriggerContext(TriggerPhase.Once, context.CancellationToken, context.Parameters);
        Api(context).WidgetsApi.SpinWheel(widget, winner => RunWhenDone(endContext, winner));
    }



    private void RunWhenDone(TriggerContext context, string winner)
    {
        if (context.CancellationToken.IsCancellationRequested) return;

        context.SetParameter("winner", winner);
        if (_whenDone.Block is ExecutableBlock block) _ = block.SafeRunAsync(context);
    }
}
