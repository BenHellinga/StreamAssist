using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class GetPartBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Get Part";
    [JsonIgnore] public override string Description => "One part of a text: Number 1 is the first, 2 the second, -1 the last. Separator is what's between the parts (empty = between words, e.g. the 2nd word of \"!so budder\" is budder; \", \" for a list). Parts are trimmed, empty ones skipped. Past the end it gives empty text.";



    // private



    private readonly ValueSlot<string> _text;
    private readonly ValueSlot<string> _separator;
    private readonly ValueSlot<int> _number;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("separator")]
    private ValueSlotData JsonSeparator
    {
        get => _separator.GetData();
        set => _separator.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("number")]
    private ValueSlotData JsonNumber
    {
        get => _number.GetData();
        set => _number.LoadData(value);
    }



    // constructor



    public GetPartBlock()
    {
        _text = new("Text");
        _separator = new("Separator");
        _number = new("Number");
        _slots = [_text, _separator, _number];

        _number.SetText("1");

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var parts = TextParts.Split(await _text.EvaluateAsync(context), await _separator.EvaluateAsync(context));
        var number = await _number.EvaluateAsync(context);
        if (number == 0) throw Fail("Number 0, the first is 1 (-1 is the last)");

        var index = number > 0 ? number - 1 : parts.Count + number;
        return index >= 0 && index < parts.Count ? parts[index] : "";
    }
}
