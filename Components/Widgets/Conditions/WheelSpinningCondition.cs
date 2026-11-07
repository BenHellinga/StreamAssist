using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Conditions;



public class WheelSpinningCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Wheel Spinning";
    [JsonIgnore] public override string Description => "True while the wheel widget named in Widget is spinning.";
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



    public WheelSpinningCondition()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _slots = [_widget];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Wheel, out var found, out var error)) throw Fail(error);
        var wheel = (WheelWidget)found;
        return wheel.IsSpinning;
    }
}
