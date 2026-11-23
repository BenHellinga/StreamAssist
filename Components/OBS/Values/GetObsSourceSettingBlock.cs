using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Values;



public class GetObsSourceSettingBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Source Setting";
    [JsonIgnore] public override string Description => "Any setting of a source by its name, like its Properties window (e.g. \"text\" for text sources), read as text, a number or true/false.";
    [JsonIgnore] public override string? Subgroup => "Scenes & Sources";



    // private



    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<string> _setting;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // what it's read as, placing it in a slot picks the slot's type
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }



    // constructor



    public GetObsSourceSettingBlock() : base(ValueKind.Text)
    {
        _source = new("Source");
        _setting = new("Setting");
        _slots = [_source, _setting];

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var source = await _source.EvaluateAsync(context);
        var setting = await _setting.EvaluateAsync(context);
        var value = await Api(context).Obs.GetSourceSettingAsync(source, setting);

        var kind = value switch { double => ValueKind.Double, bool => ValueKind.Bool, _ => ValueKind.Text };
        if (!ValueTypes.TryConvert(value, kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
