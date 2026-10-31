using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class PickRandomBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Pick Random";
    [JsonIgnore] public override string Description => "Gives one of its texts, picked at random each time (typing in the empty one adds the next, {name} works). Only the picked one is read, so a block in another one doesn't run.";



    // private



    private readonly ValueSlotList _choices;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlotList Choices => _choices;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("choices")]
    private List<ValueSlotData> JsonChoices
    {
        get => _choices.GetData();
        set => _choices.LoadData(value);
    }



    // constructor



    public PickRandomBlock()
    {
        _choices = new("");
        _slots = [_choices];

        TrackSlots();
    }



    // compute



    protected override Task<string> ComputeAsync(TriggerContext context)
    {
        var filled = _choices.Items.OfType<ValueSlot<string>>().Where(c => c.Text != "" || c.BlockSlot.Block != null).ToArray();
        if (filled.Length == 0) throw Fail("nothing to pick from");

        return filled[Random.Shared.Next(filled.Length)].EvaluateAsync(context);
    }
}
