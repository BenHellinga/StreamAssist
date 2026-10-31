using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class ConvertBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Convert";
    [JsonIgnore] public override string Description => "Turns a value into another type: text into a number or true/false (fails if it isn't one), a decimal into a whole number (drops the decimals), anything into text.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Parameters;



    // private



    private readonly AnyValueSlot _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // what it turns the value into, placing it in a slot picks the slot's type
    [ConfigurableParameter("To", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string To
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetToMenuItems() => ["Text", "Int", "Double", "Bool", "Char"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool, ValueKind.Char];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public ConvertBlock() : base(ValueKind.Text)
    {
        _value = new("Value");
        _slots = [_value];

        TrackSlots();
    }



    // compute



    // the rules are all in ValueTypes.TryConvert, typed text counts as text
    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var (kind, value) = await _value.EvaluateRawAsync(context);

        if (!ValueTypes.TryConvert(value, kind ?? ValueKind.Text, Kind, out var result, out var error))
            throw Fail(error);

        return result;
    }
}
