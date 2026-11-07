using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class SetBounceImageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Bounce Image";
    [JsonIgnore] public override string Description => "Swaps the idle or talking image of the mic bounce widget named in Widget for another file inside assets (e.g. Images/hat.png). An empty talking image uses the idle one.";
    [JsonIgnore] public override string? Subgroup => "Mic Bounce";



    // private



    private readonly MenuSlot _widget;
    private readonly MenuSlot _which;
    private readonly ValueSlot<string> _image;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetWhichMenuItems() => ["Idle", "Talking"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("which")]
    private ValueSlotData JsonWhich
    {
        get => _which.GetData();
        set => _which.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("image")]
    private ValueSlotData JsonImage
    {
        get => _image.GetData();
        set => _image.LoadData(value);
    }



    // constructor



    public SetBounceImageAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.MicBounce);
        _which = new("Which", GetWhichMenuItems, "Idle");
        _image = new("Image");
        _slots = [_widget, _which, _image];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.MicBounce, out var found, out var error)) throw Fail(error);
        var bounce = (MicBounceWidget)found;
        var which = await _which.EvaluateAsync(context);
        var image = await _image.EvaluateAsync(context);
        if (which == "Talking") bounce.SetTalkingImage(image);
        else bounce.SetIdleImage(image);
    }
}
