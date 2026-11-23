using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Actions;



public class RemovePartyItemAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Remove Party Item";
    [JsonIgnore] public override string Description => "Takes every copy of the file in File off the image party widget named in Widget (case ignored). Not on it: an error.";
    [JsonIgnore] public override string? Subgroup => "Image Party";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _file;
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

    [JsonInclude] [JsonPropertyName("file")]
    private ValueSlotData JsonFile
    {
        get => _file.GetData();
        set => _file.LoadData(value);
    }



    // constructor



    public RemovePartyItemAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.ImageParty);
        _file = new("File");
        _slots = [_widget, _file];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var widget = await _widget.EvaluateAsync(context);
        Api(context).Widgets.RemovePartyItems(widget, await _file.EvaluateAsync(context));
    }
}
