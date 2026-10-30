using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



// + − × ÷ over a growing list of numbers, worked out like C#: whole numbers stay whole (7 ÷ 2 = 3), a decimal anywhere
// makes the answer a decimal. What it gives (whole number or decimal) follows the slot it's dropped in, no dropdown
public class OperationBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Operation";
    [JsonIgnore] public override string Description => "Works through the numbers top to bottom: + adds them all, × multiplies them, − takes each one after the first away from it, ÷ divides by each one after the first. Like C#, if every number is whole the answer is whole (7 ÷ 2 = 3), a decimal anywhere makes it a decimal. Typed numbers can use parameters, e.g. {cost} + 1.";



    // private



    private string _operator;
    private readonly ValueSlotList _numbers;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Op", ConfiguratorType.Menu)] [JsonPropertyName("operator")]
    public string Operator
    {
        get => _operator;
        set => SetParameter(ref _operator, value);
    }

    public static List<string> GetOperatorMenuItems() => ["+", "−", "×", "÷"];

    [JsonIgnore] public ValueSlotList Numbers => _numbers;
    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Int, ValueKind.Double];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    // what it gives isn't a setting, it comes from the slot it was dropped in, saved so a loaded block still fits there
    [JsonInclude] [JsonPropertyName("kind")]
    private string JsonKind
    {
        get => KindName;
        set => KindName = value;
    }

    [JsonInclude] [JsonPropertyName("numbers")]
    private List<ValueSlotData> JsonNumbers
    {
        get => _numbers.GetData();
        set => _numbers.LoadData(value);
    }



    // constructor



    public OperationBlock() : base(ValueKind.Double)
    {
        _operator = "+";
        _numbers = new("", ValueKind.Double);
        _slots = [_numbers];

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var numbers = await _numbers.EvaluateFilledAsync<double>(context);
        if (numbers.Count == 0) throw Fail("no numbers");

        // like C#: only whole numbers means whole number maths, so dividing drops the decimals at each step
        var allWhole = numbers.All(n => n == Math.Truncate(n));

        var result = numbers[0];
        foreach (var number in numbers.Skip(1))
        {
            result = _operator switch
            {
                "+" => result + number,
                "−" => result - number,
                "×" => result * number,
                "÷" when number == 0 => throw Fail("can't divide by zero"),
                "÷" => allWhole ? Math.Truncate(result / number) : result / number,
                _ => throw Fail($"unknown op '{_operator}'"),
            };
        }

        if (Kind == ValueKind.Double) return result;

        // in a whole number slot a decimal answer drops its decimals (like Convert)
        var whole = Math.Truncate(result);
        if (whole is > int.MaxValue or < int.MinValue) throw Fail($"{whole} too big for a whole number");
        return (int)whole;
    }
}
