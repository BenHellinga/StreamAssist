using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Actions;



public class SetParametersAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Parameters";
    [JsonIgnore] public override string Description => "Set Parameter for several at once: a name on each row with its value to the right, set top to bottom (a later row can use an earlier one's {name}). Only for this run, nothing is kept.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Parameters;



    // private



    private readonly ValueSlotPairList _rows;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlotPairList Rows => _rows;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("rows")]
    private List<ValueSlotPairData> JsonRows
    {
        get => _rows.GetData();
        set => _rows.LoadData(value);
    }



    // constructor



    public SetParametersAction()
    {
        _rows = new("", "Name…");
        _slots = [_rows];

        TrackSlots();
    }



    // execute



    // one row at a time, so a later row's {name} sees the ones set above it
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        foreach (var row in _rows.Items)
        {
            if (row.IsEmpty) continue;

            var name = (await row.Key.EvaluateAsync(context)).Trim();
            if (name == "") throw Fail("a row needs a name");
            if (!TriggerContext.IsValidName(name)) throw Fail($"'{name}' isn't a valid name, use letters, digits, _");

            var (_, value) = await row.Value.EvaluateRawAsync(context);
            context.SetParameter(name, ValueTypes.ToText(value));
        }
    }
}
