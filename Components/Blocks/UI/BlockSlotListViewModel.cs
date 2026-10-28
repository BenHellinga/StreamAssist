using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for a list slot: each block's card with a drop zone above it, plus one at the end
public class BlockSlotListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly BlockSlotList _list;
    private readonly ObservableCollection<BlockViewModel> _blocks;



    // public



    public string Label => _list.Label;
    public bool HasLabel => !string.IsNullOrEmpty(_list.Label);
    public ObservableCollection<BlockViewModel> Blocks => _blocks;



    // constructor



    public BlockSlotListViewModel(BlockSlotList list)
    {
        _list = list;
        _blocks = [];

        foreach (var block in _list.Blocks)
            InsertBlockViewModel(block, _blocks.Count);

        SubscribeToBlockSlotList(_list);
    }



    // drag drop



    public bool CanAcceptDropPayload(object payload) =>
        payload is BlockDragPayload blockPayload
        && _list.Accepts(blockPayload.Block)
        && !blockPayload.Block.ContainsSlot(_list);



    // zoneContext is the block the drop zone sits above, or null for the zone at the end
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BlockDragPayload blockPayload) return;

        var index = zoneContext is BlockViewModel beforeBlock ? _blocks.IndexOf(beforeBlock) : _blocks.Count;
        if (index < 0) index = _blocks.Count;

        var copy = BlockResolver.DeepCopy(blockPayload.Block);
        if (copy == null) return;

        if (!_list.TryInsertBlock(copy, index))
        {
            copy.Dispose();
            return;
        }

        blockPayload.RemoveFromSource?.Invoke();
    }



    // model events



    private void OnBlockInserted(BlockModel block, int index) => InsertBlockViewModel(block, index);



    private void OnBlockRemoved(BlockModel block)
    {
        var viewModel = _blocks.FirstOrDefault(b => b.RepresentsModel(block));
        if (viewModel == null) return;

        _blocks.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private void InsertBlockViewModel(BlockModel block, int index) =>
        _blocks.Insert(index, new BlockViewModel(block, () => _list.RemoveBlock(block)));



    private void SubscribeToBlockSlotList(BlockSlotList list)
    {
        list.BlockInserted += OnBlockInserted;
        list.BlockRemoved += OnBlockRemoved;
    }



    private void UnsubscribeFromBlockSlotList(BlockSlotList list)
    {
        list.BlockInserted -= OnBlockInserted;
        list.BlockRemoved -= OnBlockRemoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromBlockSlotList(_list);

        foreach (var block in _blocks)
            block.Dispose();
    }
}
