using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsSourceSettingBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Source Setting";
    [JsonIgnore] public override string Description => "Any setting of a source by its name, like its Properties window (e.g. \"text\" for text sources), read as text, a number or true/false.";



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
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var setting = await _setting.EvaluateAsync(context);
        if (setting.Trim() == "") throw Fail("needs a setting name");

        var (ok, json) = await obs.TryGetSourceSettingAsync(source, setting.Trim());
        if (!ok) throw new BlockFailedException("setting not read");

        // what OBS stores (text, a number, true/false; anything else as its json), then read as the chosen type
        (ValueKind kind, object value) = json.ValueKind switch
        {
            JsonValueKind.String => (ValueKind.Text, (object)json.GetString()!),
            JsonValueKind.Number => (ValueKind.Double, json.GetDouble()),
            JsonValueKind.True or JsonValueKind.False => (ValueKind.Bool, json.GetBoolean()),
            _ => (ValueKind.Text, json.GetRawText()),
        };

        if (!ValueTypes.TryConvert(value, kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
