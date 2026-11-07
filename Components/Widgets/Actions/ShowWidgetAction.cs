using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class ShowWidgetAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Show Widget";
    [JsonIgnore] public override string Description => "Shows, hides or toggles the widget named in Widget (the same as its on / off button): hidden, it doesn't run (an empty page, no sound or spins), its settings can still be changed.";



    // private



    private readonly MenuSlot _widget;
    private readonly MenuSlot _set;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetSetMenuItems() => ["Show", "Hide", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("set")]
    private ValueSlotData JsonSet
    {
        get => _set.GetData();
        set => _set.LoadData(value);
    }



    // constructor



    public ShowWidgetAction()
    {
        _widget = WidgetBlocks.WidgetSlot(null);
        _set = new("Set", GetSetMenuItems, "Toggle");
        _slots = [_widget, _set];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), null, out var widget, out var error)) throw Fail(error);
        var set = await _set.EvaluateAsync(context);
        widget.SetIsDisabled(set switch { "Show" => false, "Hide" => true, _ => !widget.IsDisabled });
    }
}
