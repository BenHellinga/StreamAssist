using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class IfBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "If";
    [JsonIgnore] public override string Description => "Runs the block under the first true condition, or the else block if none are true.";



    // private



    private readonly BlockBranchList _branches;
    private readonly BlockSlot _else;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("branches")]
    private List<BlockBranchData> JsonBranches
    {
        get => _branches.GetData();
        set => _branches.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("else")]
    private PendingBlockData? JsonElse
    {
        get => _else.GetData();
        set => _else.LoadData(value);
    }



    // constructor



    public IfBlock()
    {
        _branches = new("");
        _else = new("else", typeof(ExecutableBlock));
        _slots = [_branches, _else];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (await _branches.RunFirstMatchAsync(context)) return;
        await _else.RunAsync(context);
    }
}
