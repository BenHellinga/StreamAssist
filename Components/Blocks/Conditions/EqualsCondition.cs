using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class EqualsCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Equals";
    [JsonIgnore] public override string Description => "True if both are the same value of the same type (whole numbers and decimals compare as numbers, text is case sensitive). Typed text is read as the type of the block on the other side.";



    // private



    private readonly AnyValueSlot _first;
    private readonly AnyValueSlot _second;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("first")]
    private ValueSlotData JsonFirst
    {
        get => _first.GetData();
        set => _first.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("second")]
    private ValueSlotData JsonSecond
    {
        get => _second.GetData();
        set => _second.LoadData(value);
    }



    // constructor



    public EqualsCondition()
    {
        _first = new("A");
        _second = new("B");
        _slots = [_first, _second];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var (firstKind, first) = await _first.EvaluateRawAsync(context);
        var (secondKind, second) = await _second.EvaluateRawAsync(context);
        return AreEqual(firstKind, first, secondKind, second);
    }



    // a null kind is typed text: it's read as the other side's kind (typed text that isn't one is just not equal),
    // both typed are compared as text. Different kinds are never equal, except int and double (5 == 5.0)
    public static bool AreEqual(ValueKind? firstKind, object first, ValueKind? secondKind, object second)
    {
        if (firstKind == null && secondKind == null) return (string)first == (string)second;

        if (firstKind == null)
        {
            if (!ValueTypes.TryParse((string)first, secondKind!.Value, out first)) return false;
            firstKind = secondKind;
        }

        if (secondKind == null)
        {
            if (!ValueTypes.TryParse((string)second, firstKind.Value, out second)) return false;
            secondKind = firstKind;
        }

        if (IsNumber(firstKind.Value) && IsNumber(secondKind.Value))
            return ToDouble(first) == ToDouble(second);

        return firstKind == secondKind && first.Equals(second);
    }



    // helpers



    private static bool IsNumber(ValueKind kind) => kind is ValueKind.Int or ValueKind.Double;



    private static double ToDouble(object number) => number is int whole ? whole : (double)number;
}
