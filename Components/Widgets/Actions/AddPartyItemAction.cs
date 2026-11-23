using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Widgets.Actions;



public class AddPartyItemAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add Party Item";
    [JsonIgnore] public override string Description => "Adds the image, gif or video in File (inside assets) to the image party widget named in Widget, Size wide and moving Speed pixels a second (before its multipliers; a Random Number block in either gives a random one), bouncing from somewhere random. The same file again is another copy. A video plays muted if Muted is ticked.";
    [JsonIgnore] public override string? Subgroup => "Image Party";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _file;
    private readonly ValueSlot<double> _size;
    private readonly ValueSlot<double> _speed;
    private bool _muted;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Muted", ConfiguratorType.Boolean)] [JsonPropertyName("muted")]
    public bool Muted
    {
        get => _muted;
        set => SetParameter(ref _muted, value);
    }

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

    [JsonInclude] [JsonPropertyName("size")]
    private ValueSlotData JsonSize
    {
        get => _size.GetData();
        set => _size.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("speed")]
    private ValueSlotData JsonSpeed
    {
        get => _speed.GetData();
        set => _speed.LoadData(value);
    }



    // constructor



    public AddPartyItemAction()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.ImageParty);
        _file = new("File");
        _size = new("Size");
        _size.SetText("200");
        _speed = new("Speed");
        _speed.SetText("200");
        _muted = false;
        _slots = [_widget, _file, _size, _speed];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var widget = await _widget.EvaluateAsync(context);
        var file = await _file.EvaluateAsync(context);
        var size = await _size.EvaluateAsync(context);
        var speed = await _speed.EvaluateAsync(context);
        Api(context).Widgets.AddPartyItem(widget, file, size, speed, _muted);
    }
}
