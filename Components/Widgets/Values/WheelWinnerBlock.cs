using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Values;



public class WheelWinnerBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Wheel Winner";
    [JsonIgnore] public override string Description => "The option the wheel widget named in Widget last landed on (empty until it has spun since the app started). Spin Wheel gives it as {winner} too.";
    [JsonIgnore] public override string? Subgroup => "Wheel";



    // private



    private readonly MenuSlot _widget;
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



    // constructor



    public WheelWinnerBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _slots = [_widget];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context) =>
        Api(context).Widgets.GetWheelWinner(await _widget.EvaluateAsync(context));
}
