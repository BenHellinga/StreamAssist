using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Values;



public class WheelOptionsBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Wheel Options";
    [JsonIgnore] public override string Description => "The options of the wheel widget named in Widget, as text joined by Separator (e.g. for For Each, or a chat message).";
    [JsonIgnore] public override string? Subgroup => "Wheel";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _separator;
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

    [JsonInclude] [JsonPropertyName("separator")]
    private ValueSlotData JsonSeparator
    {
        get => _separator.GetData();
        set => _separator.LoadData(value);
    }



    // constructor



    public WheelOptionsBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Wheel);
        _separator = new("Separator");
        _separator.SetText(", ");
        _slots = [_widget, _separator];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Wheel, out var found, out var error)) throw Fail(error);
        var wheel = (WheelWidget)found;
        return string.Join(await _separator.EvaluateAsync(context), wheel.FilledOptions.Select(o => o.Text));
    }
}
