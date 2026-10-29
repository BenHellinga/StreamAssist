using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



public class JoinBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Join";
    [JsonIgnore] public override string Description => "Puts its parts together top to bottom into one text (like $\"...\" in code). Each part is typed text ({name} works) or a block (numbers and true/false become text). Spaces aren't added for you.";



    // private



    private readonly ValueSlotList _parts;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("parts")]
    private List<ValueSlotData> JsonParts
    {
        get => _parts.GetData();
        set => _parts.LoadData(value);
    }



    // constructor



    public JoinBlock()
    {
        _parts = new("");
        _slots = [_parts];

        TrackSlots();
    }



    // compute



    protected override Task<string> ComputeAsync(TriggerContext context) => _parts.EvaluateJoinedAsync(context);
}
