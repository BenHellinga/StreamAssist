using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class ReplaceTextBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Replace Text";
    [JsonIgnore] public override string Description => "The text with every Find replaced by Replace With (empty Replace With removes it). Case is ignored unless Match Case is picked.";



    // private



    private string _case;
    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<string> _find;
    private readonly ValueSlot<string> _replacement;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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

    [JsonInclude] [JsonPropertyName("replacement")]
    private ValueSlotData JsonReplacement
    {
        get => _replacement.GetData();
        set => _replacement.LoadData(value);
    }



    // constructor



    public ReplaceTextBlock()
    {
        _case = "Ignore Case";
        _text = new("Text");
        _find = new("Find");
        _replacement = new("Replace With");
        _slots = [_text, _find, _replacement];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var text = await _text.EvaluateAsync(context);
        var find = await _find.EvaluateAsync(context);
        var replacement = await _replacement.EvaluateAsync(context);
        if (find == "") throw Fail("Find is empty");

        return text.Replace(find, replacement, _case == "Match Case" ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }
}
