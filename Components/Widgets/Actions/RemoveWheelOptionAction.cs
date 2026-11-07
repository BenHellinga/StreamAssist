using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class RemoveWheelOptionAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Remove Wheel Option";
    [JsonIgnore] public override string Description => "Takes an option (by its text, case ignored) off the wheel widget named in Widget.";
    [JsonIgnore] public override string? Subgroup => "Wheel";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _option;
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

    [JsonInclude] [JsonPropertyName("option")]
    private ValueSlotData JsonOption
    {
        get => _option.GetData();
        set => _option.LoadData(value);
    }



    // constructor



    public RemoveWheelOptionAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _option = new("Option");
        _slots = [_widget, _option];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Wheel, out var found, out var error)) throw Fail(error);
        var wheel = (WheelWidget)found;
        if (!wheel.TryRemoveOption(await _option.EvaluateAsync(context))) throw new BlockFailedException("option not removed");
    }
}
