using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Conditions;



public class TextContainsCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Text Contains";
    [JsonIgnore] public override string Description => "True if the text contains Find, or starts or ends with it (Check). Case is ignored unless Match Case is picked. Parameters work in both, e.g. {message}.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Text;



    // private



    private string _check;
    private string _case;
    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<string> _find;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Check", ConfiguratorType.Menu)] [JsonPropertyName("check")]
    public string Check
    {
        get => _check;
        set => SetParameter(ref _check, value);
    }

    public static List<string> GetCheckMenuItems() => ["Contains", "Starts With", "Ends With"];

    [ConfigurableParameter("Case", ConfiguratorType.Menu)] [JsonPropertyName("case")]
    public string Case
    {
        get => _case;
        set => SetParameter(ref _case, value);
    }

    public static List<string> GetCaseMenuItems() => ["Ignore Case", "Match Case"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("find")]
    private ValueSlotData JsonFind
    {
        get => _find.GetData();
        set => _find.LoadData(value);
    }



    // constructor



    public TextContainsCondition()
    {
        _check = "Contains";
        _case = "Ignore Case";
        _text = new("Text");
        _find = new("Find");
        _slots = [_text, _find];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var text = await _text.EvaluateAsync(context);
        var find = await _find.EvaluateAsync(context);
        var comparison = _case == "Match Case" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return _check switch
        {
            "Starts With" => text.StartsWith(find, comparison),
            "Ends With" => text.EndsWith(find, comparison),
            _ => text.Contains(find, comparison),
        };
    }
}
