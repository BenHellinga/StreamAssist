using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsTransformAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Transform";
    [JsonIgnore] public override string Description => "Moves, resizes, rotates or crops a source in a scene: pick what to set, in pixels (degrees for Rotation, 1 = own size for Scale). Width and Height resize it by changing its scale. Scene left empty = the live scene.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<double> _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Setting", ConfiguratorType.Menu)] [JsonPropertyName("setting")]
    public string Setting
    {
        get => _setting;
        set => SetParameter(ref _setting, value);
    }

    public static List<string> GetSettingMenuItems() => ObsTransforms.Names.ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



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

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetObsTransformAction()
    {
        _setting = "Position X";
        _scene = new("Scene");
        _source = new("Source");
        _value = new("Value");
        _slots = [_scene, _source, _value];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var transform = ObsTransforms.Find(_setting) ?? throw Fail($"unknown setting '{_setting}'");
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);
        var value = await _value.EvaluateAsync(context);

        if (!(await obs.TrySetTransformAsync(scene, source, transform, value)).Ok) throw new BlockFailedException("transform not set");
    }
}
