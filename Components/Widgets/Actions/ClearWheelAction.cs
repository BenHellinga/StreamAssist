using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class ClearWheelAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Clear Wheel";
    [JsonIgnore] public override string Description => "Takes every option off the wheel widget named in Widget.";
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



    public ClearWheelAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _slots = [_widget];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context) =>
        Api(context).Widgets.ClearWheel(await _widget.EvaluateAsync(context));
}
