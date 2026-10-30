using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsTransformBlock : ValueBlock<double>
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Transform";
    [JsonIgnore] public override string Description => "Where a source is in a scene, or its size, rotation or crop: pick which, in pixels (degrees for Rotation, 1 = own size for Scale). Scene left empty = the live scene.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
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



    // constructor



    public GetObsTransformBlock()
    {
        _setting = "Position X";
        _scene = new("Scene");
        _source = new("Source");
        _slots = [_scene, _source];

        TrackSlots();
    }



    // compute



    protected override async Task<double> ComputeAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var transform = ObsTransforms.Find(_setting) ?? throw Fail($"unknown setting '{_setting}'");
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);

        var (ok, value) = await obs.TryGetTransformAsync(scene, source, transform);
        return ok ? value : throw new BlockFailedException("transform not read");
    }
}
