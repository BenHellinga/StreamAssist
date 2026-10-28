using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// a block in the block list, dragging it places a copy
public sealed class BlockCardViewModel : ViewModelBase, IDraggable
{
    // private



    private readonly BlockModel _block;



    // public



    public string Name => _block.Name;
    public string Description => _block.Description;



    // constructor



    public BlockCardViewModel(BlockModel block)
    {
        _block = block;
    }



    // drag drop



    public object? GetDragPayload() => new BlockDragPayload(_block);



    // disposal



    public override void Dispose() => _block.Dispose();
}
