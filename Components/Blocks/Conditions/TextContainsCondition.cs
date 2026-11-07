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



    private readonly MenuSlot _check;
    private readonly MenuSlot _case;
    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<string> _find;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetCheckMenuItems() => ["Contains", "Starts With", "Ends With"];


    public static List<string> GetCaseMenuItems() => ["Ignore Case", "Match Case"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("check")]
    private ValueSlotData JsonCheck
    {
        get => _check.GetData();
        set => _check.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("case")]
    private ValueSlotData JsonCase
    {
        get => _case.GetData();
        set => _case.LoadData(value);
    }

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
        _check = new("Check", GetCheckMenuItems, "Contains");
        _case = new("Case", GetCaseMenuItems, "Ignore Case");
        _text = new("Text");
        _find = new("Find");
        _slots = [_check, _case, _text, _find];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var check = await _check.EvaluateAsync(context);
        var caseOption = await _case.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);
        var find = await _find.EvaluateAsync(context);
        var comparison = caseOption == "Match Case" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return check switch
        {
            "Starts With" => text.StartsWith(find, comparison),
            "Ends With" => text.EndsWith(find, comparison),
            _ => text.Contains(find, comparison),
        };
    }
}
