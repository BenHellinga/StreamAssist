using System;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.UI;



// data class for a block being dragged, the drop target places a copy then calls removeFromSource
public sealed class BlockDragPayload
{
    // private



    private readonly BlockModel _block;
    private readonly Action? _removeFromSource;



    // public



    public BlockModel Block => _block;
    public Action? RemoveFromSource => _removeFromSource;



    // constructor



    public BlockDragPayload(BlockModel block, Action? removeFromSource = null)
    {
        _block = block;
        _removeFromSource = removeFromSource;
    }
}
