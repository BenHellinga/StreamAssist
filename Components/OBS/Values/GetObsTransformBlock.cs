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



    private readonly MenuSlot _setting;
    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetSettingMenuItems() => ObsTransforms.Names.ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
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



    public GetObsTransformBlock()
    {
        _setting = new("Setting", GetSettingMenuItems, "Position X");
        _scene = new("Scene");
        _source = new("Source");
        _slots = [_setting, _scene, _source];

        TrackSlots();
    }



    // compute



    protected override async Task<double> ComputeAsync(TriggerContext context)
    {
        var setting = await _setting.EvaluateAsync(context);
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var transform = ObsTransforms.Find(setting) ?? throw Fail($"unknown setting '{setting}'");
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);

        var (ok, value) = await obs.TryGetTransformAsync(scene, source, transform);
        return ok ? value : throw new BlockFailedException("transform not read");
    }
}
