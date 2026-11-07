using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class TextLengthBlock : ValueBlock<int>
{
    // info



    [JsonIgnore] public override string Name => "Text Length";
    [JsonIgnore] public override string Description => "How many characters or words a text has (words are separated by spaces).";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Text;



    // private



    private readonly MenuSlot _count;
    private readonly ValueSlot<string> _text;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetCountMenuItems() => ["Characters", "Words"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("count")]
    private ValueSlotData JsonCount
    {
        get => _count.GetData();
        set => _count.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }



    // constructor



    public TextLengthBlock()
    {
        _count = new("Count", GetCountMenuItems, "Characters");
        _text = new("Text");
        _slots = [_count, _text];

        TrackSlots();
    }



    // compute



    protected override async Task<int> ComputeAsync(TriggerContext context)
    {
        var count = await _count.EvaluateAsync(context);
        var text = await _text.EvaluateAsync(context);
        return count == "Words" ? TextParts.Split(text, "").Count : text.Length;
    }
}
