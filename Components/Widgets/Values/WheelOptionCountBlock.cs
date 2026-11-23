using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Values;



public class WheelOptionCountBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Wheel Option Count";
    [JsonIgnore] public override string Description => "How many options the wheel widget named in Widget has.";
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



    public WheelOptionCountBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _slots = [_widget];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context) =>
        Api(context).Widgets.GetWheelOptions(await _widget.EvaluateAsync(context)).Count;
}
