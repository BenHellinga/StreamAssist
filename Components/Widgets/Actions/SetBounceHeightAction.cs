using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class SetBounceHeightAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Bounce Height";
    [JsonIgnore] public override string Description => "How high the mic bounce widget named in Widget rises at full loudness, in pixels (0 to 2000), e.g. from an Expression.";
    [JsonIgnore] public override string? Subgroup => "Mic Bounce";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<double> _height;
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

    [JsonInclude] [JsonPropertyName("height")]
    private ValueSlotData JsonHeight
    {
        get => _height.GetData();
        set => _height.LoadData(value);
    }



    // constructor



    public SetBounceHeightAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.MicBounce);
        _height = new("Height");
        _height.SetText("40");
        _slots = [_widget, _height];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.MicBounce, out var found, out var error)) throw Fail(error);
        var bounce = (MicBounceWidget)found;
        if (!bounce.TrySetHeight(await _height.EvaluateAsync(context))) throw new BlockFailedException("height not set");
    }
}
