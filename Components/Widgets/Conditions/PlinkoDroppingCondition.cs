using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Conditions;



public class PlinkoDroppingCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Plinko Dropping";
    [JsonIgnore] public override string Description => "True while the plinko widget named in Widget has balls falling or waiting to drop in.";
    [JsonIgnore] public override string? Subgroup => "Plinko";



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



    public PlinkoDroppingCondition()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Plinko);
        _slots = [_widget];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Plinko, out var found, out var error)) throw Fail(error);
        var plinko = (PlinkoWidget)found;
        return plinko.IsDropping;
    }
}
