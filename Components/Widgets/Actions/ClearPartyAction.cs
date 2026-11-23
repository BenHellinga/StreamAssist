using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class ClearPartyAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Clear Party";
    [JsonIgnore] public override string Description => "Takes every item off the image party widget named in Widget.";
    [JsonIgnore] public override string? Subgroup => "Image Party";



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



    public ClearPartyAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.ImageParty);
        _slots = [_widget];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.ImageParty, out var found, out var error)) throw Fail(error);
        var party = (ImagePartyWidget)found;
        party.ClearItems();
    }
}
