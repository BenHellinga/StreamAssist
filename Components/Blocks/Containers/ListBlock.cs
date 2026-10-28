using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Containers;



public class ListBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "List";
    [JsonIgnore] public override string Description => "Runs every block inside, top to bottom, waiting for each to finish.";



    // private



    private readonly BlockSlotList _children;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("children")]
    private List<PendingBlockData> JsonChildren
    {
        get => _children.GetData();
        set => _children.LoadData(value);
    }



    // constructor



    public ListBlock()
    {
        _children = new("", typeof(ExecutableBlock));
        _slots = [_children];

        TrackSlots();
    }



    // execute



    protected override Task ExecuteAsync(TriggerContext context) => _children.RunAllAsync(context);
}
