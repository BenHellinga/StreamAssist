using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class CompareCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Compare";
    [JsonIgnore] public override string Description => "Compares two numbers, e.g. viewers ≥ 50 (whole numbers and decimals both fit). For equal, use Equals.";



    // private



    private string _operator;
    private readonly ValueSlot<double> _first;
    private readonly ValueSlot<double> _second;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Is A", ConfiguratorType.Menu)] [JsonPropertyName("operator")]
    public string Operator
    {
        get => _operator;
        set => SetParameter(ref _operator, value);
    }

    public static List<string> GetOperatorMenuItems() => ["<", "≤", ">", "≥"];

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



    public CompareCondition()
    {
        _operator = "≥";
        _first = new("A");
        _second = new("B");
        _slots = [_first, _second];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var first = await _first.EvaluateAsync(context);
        var second = await _second.EvaluateAsync(context);

        return _operator switch
        {
            "<" => first < second,
            "≤" => first <= second,
            ">" => first > second,
            _ => first >= second,
        };
    }
}
