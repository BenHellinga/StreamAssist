using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class OnReleasedBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "On Released";
    [JsonIgnore] public override string Description => "Runs the block inside when the trigger stops (key up) or fires once.";



    // private



    private readonly BlockSlot _child;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("child")]
    private PendingBlockData? JsonChild
    {
        get => _child.GetData();
        set => _child.LoadData(value);
    }



    // constructor



    public OnReleasedBlock()
    {
        _child = new("", typeof(ExecutableBlock));
        _slots = [_child];

        TrackSlots();
    }



    // execute



    protected override Task ExecuteAsync(TriggerContext context)
    {
        if (context.Phase == TriggerPhase.Start) return Task.CompletedTask;
        return _child.RunAsync(context);
    }
}
