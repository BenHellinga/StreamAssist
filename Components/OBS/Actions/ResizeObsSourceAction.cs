using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class ResizeObsSourceAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Resize OBS Source";
    [JsonIgnore] public override string Description => "Grows or shrinks a source in a scene while one of its points stays still (Keep Fixed, e.g. Bottom Right for something in the bottom-right corner). Change by % takes Percent (20 grows by 20%, -20 shrinks by 20%), Set size to takes Width and Height in pixels. For a source inside a group, Scene is the group's name. An empty Scene means the live scene.";
    [JsonIgnore] public override string? Subgroup => "Transform";



    // private



    private readonly MenuSlot _mode;
    private readonly MenuSlot _anchor;
    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<double> _percent;
    private readonly ValueSlot<double> _width;
    private readonly ValueSlot<double> _height;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetModeMenuItems() => ["Change by %", "Set size to"];


    public static List<string> GetAnchorMenuItems() => ObsManager.Anchors.Select(a => a.Name).ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("mode")]
    private ValueSlotData JsonMode
    {
        get => _mode.GetData();
        set => _mode.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("anchor")]
    private ValueSlotData JsonAnchor
    {
        get => _anchor.GetData();
        set => _anchor.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("scene")]
    private ValueSlotData JsonScene { get => _scene.GetData(); set => _scene.LoadData(value); }

    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource { get => _source.GetData(); set => _source.LoadData(value); }

    [JsonInclude] [JsonPropertyName("percent")]
    private ValueSlotData JsonPercent { get => _percent.GetData(); set => _percent.LoadData(value); }

    [JsonInclude] [JsonPropertyName("width")]
    private ValueSlotData JsonWidth { get => _width.GetData(); set => _width.LoadData(value); }

    [JsonInclude] [JsonPropertyName("height")]
    private ValueSlotData JsonHeight { get => _height.GetData(); set => _height.LoadData(value); }



    // constructor



    public ResizeObsSourceAction()
    {
        _mode = new("Mode", GetModeMenuItems, "Change by %");
        _anchor = new("Keep Fixed", GetAnchorMenuItems, "Bottom Right");
        _scene = new("Scene");
        _source = new("Source");
        _percent = new("Percent");
        _width = new("Width");
        _height = new("Height");
        _slots = [_mode, _anchor, _scene, _source, _percent, _width, _height];

        _percent.SetText("20");
        _width.SetText("100");
        _height.SetText("100");

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var mode = await _mode.EvaluateAsync(context);
        var anchor = await _anchor.EvaluateAsync(context);
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);

        System.Func<double, double, (double, double)> newSize;
        if (mode == "Set size to")
        {
            var (width, height) = (await _width.EvaluateAsync(context), await _height.EvaluateAsync(context));
            newSize = (_, _) => (width, height);
        }
        else
        {
            var factor = 1 + await _percent.EvaluateAsync(context) / 100;
            if (factor <= 0) throw Fail("can't shrink by 100% or more");
            newSize = (width, height) => (width * factor, height * factor);
        }

        if (!(await obs.TryResizeAsync(scene, source, newSize, anchor)).Ok) throw new BlockFailedException("not resized");
    }
}
