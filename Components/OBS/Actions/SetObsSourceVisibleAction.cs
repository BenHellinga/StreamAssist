using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsSourceVisibleAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Source Visible";
    [JsonIgnore] public override string Description => "Shows, hides or toggles a source in a scene (the eye in OBS). Scene left empty = the live scene.";



    // private



    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Show", "Hide", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("scene")]
    private ValueSlotData JsonScene
    {
        get => _scene.GetData();
        set => _scene.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }



    // constructor



    public SetObsSourceVisibleAction()
    {
        _change = new("Do", GetChangeMenuItems, "Toggle");
        _scene = new("Scene");
        _source = new("Source");
        _slots = [_change, _scene, _source];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var changeOption = await _change.EvaluateAsync(context);
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);
        var change = changeOption == "Show" ? ObsToggle.On : changeOption == "Hide" ? ObsToggle.Off : ObsToggle.Toggle;

        if (!(await obs.TrySetSourceVisibleAsync(scene, source, change)).Ok) throw new BlockFailedException("source not shown/hidden");
    }
}
