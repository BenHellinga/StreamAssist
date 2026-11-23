using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Conditions;



public class ObsSourceVisibleCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "OBS Source Visible";
    [JsonIgnore] public override string Description => "True if a source is showing in a scene (its eye is on in OBS). Scene left empty = the live scene.";
    [JsonIgnore] public override string? Subgroup => "Scenes & Sources";



    // private



    private readonly ValueSlot<string> _scene;
    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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



    public ObsSourceVisibleCondition()
    {
        _scene = new("Scene");
        _source = new("Source");
        _slots = [_scene, _source];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var scene = await _scene.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);
        return await Api(context).Obs.IsSourceVisibleAsync(source, scene);
    }
}
