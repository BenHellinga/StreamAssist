using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



// a text with {name}s, and name / value rows below it that only this block sees: e.g. "{1} - {2}" with 1 and 2 set
// from blocks. The rows are worked out top to bottom (a row can use an earlier one), then the text is filled in.
// The run's own parameters ({user}, {quote}, ...) work too; a row with the same name wins here only
public class StringBuilderBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "String Builder";
    [JsonIgnore] public override string Description => "Builds a text from the one typed at the top, filling in each {name}. The rows below give names that only this block sees, e.g. {1} and {2} set from other blocks, then \"{1} - {2}\" at the top. Rows are worked out top to bottom, and the run's parameters ({user}, ...) work too. Use it inside Send Chat Message, an Expression, or anything that takes text.";



    // private



    private readonly ValueSlot<string> _text;
    private readonly ValueSlotPairList _values;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Text => _text;
    [JsonIgnore] public ValueSlotPairList Values => _values;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("text")]
    private ValueSlotData JsonText
    {
        get => _text.GetData();
        set => _text.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("values")]
    private List<ValueSlotPairData> JsonValues
    {
        get => _values.GetData();
        set => _values.LoadData(value);
    }



    // constructor



    public StringBuilderBlock()
    {
        _text = new("");
        _values = new("", "Name…");
        _slots = [_text, _values];

        TrackSlots();
    }



    // compute



    // the rows go into a copy of the run's parameters, so nothing after this block sees them
    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var scope = new TriggerContext(context.Phase, context.CancellationToken, context.Parameters);

        foreach (var row in _values.Items)
        {
            if (row.IsEmpty) continue;

            var name = (await row.Key.EvaluateAsync(scope)).Trim();
            if (name == "") throw Fail("a row needs a name");
            if (!TriggerContext.IsValidName(name)) throw Fail($"'{name}' isn't a valid name, use letters, digits, _");

            var (_, value) = await row.Value.EvaluateRawAsync(scope);
            scope.SetParameter(name, ValueTypes.ToText(value));
        }

        return await _text.EvaluateAsync(scope);
    }
}
