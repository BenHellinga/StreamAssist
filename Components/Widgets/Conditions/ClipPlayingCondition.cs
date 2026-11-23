using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Conditions;



public class ClipPlayingCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Clip Playing";
    [JsonIgnore] public override string Description => "True while the clip widget named in Widget is playing a clip.";
    [JsonIgnore] public override string? Subgroup => "Clip";



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



    public ClipPlayingCondition()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Clip);
        _slots = [_widget];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context) =>
        Api(context).Widgets.IsClipPlaying(await _widget.EvaluateAsync(context));
}
