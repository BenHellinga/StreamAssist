using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class ChangeTextBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Change Text";
    [JsonIgnore] public override string Description => "The text in upper case, lower case, trimmed (no spaces at the ends) or capitalized (the first letter upper case).";



    // private



    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _text;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Upper Case", "Lower Case", "Trim", "Capitalize"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }



    // constructor



    public ChangeTextBlock()
    {
        _change = new("Do", GetChangeMenuItems, "Upper Case");
        _text = new("Text");
        _slots = [_change, _text];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);

        return change switch
        {
            "Lower Case" => text.ToLowerInvariant(),
            "Trim" => text.Trim(),
            "Capitalize" => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..],
            _ => text.ToUpperInvariant(),
        };
    }
}
