using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class AddWheelOptionAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add Wheel Option";
    [JsonIgnore] public override string Description => "Adds an option to the wheel widget named in Widget (saved with it). Weight is how likely it is and how wide its slice (2 is twice as likely as 1). Color like #FF8800, empty takes the next one from the palette. Already on the wheel: nothing changes.";
    [JsonIgnore] public override string? Subgroup => "Wheel";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _option;
    private readonly ValueSlot<double> _weight;
    private readonly ValueSlot<string> _color;
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

    [JsonInclude] [JsonPropertyName("weight")]
    private ValueSlotData JsonWeight
    {
        get => _weight.GetData();
        set => _weight.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("color")]
    private ValueSlotData JsonColor
    {
        get => _color.GetData();
        set => _color.LoadData(value);
    }



    // constructor



    public AddWheelOptionAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _option = new("Option");
        _weight = new("Weight");
        _weight.SetText("1");
        _color = new("Color");
        _slots = [_widget, _option, _weight, _color];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Wheel, out var found, out var error)) throw Fail(error);
        var wheel = (WheelWidget)found;
        var option = await _option.EvaluateAsync(context);
        var weight = await _weight.EvaluateAsync(context);
        var color = await _color.EvaluateAsync(context);
        if (!wheel.TryAddOption(option, weight, color)) throw new BlockFailedException("option not added");
    }
}
