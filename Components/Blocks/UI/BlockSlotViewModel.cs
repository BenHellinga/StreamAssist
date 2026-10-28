using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for a single slot: a drop zone when empty, the block's card when filled
public partial class BlockSlotViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly BlockSlot _slot;

    [ObservableProperty] private BlockViewModel? _block;



    // public



    public string Label => _slot.Label;
    public bool HasLabel => !string.IsNullOrEmpty(_slot.Label);
    public bool IsConditionSlot => typeof(ConditionBlock).IsAssignableFrom(_slot.AcceptedType);



    // constructor



    public BlockSlotViewModel(BlockSlot slot)
    {
        _slot = slot;
        _block = null;

        SetBlockViewModel(_slot.Block);
        SubscribeToBlockSlot(_slot);
    }



    // drag drop



    // only an empty slot shows a drop zone, and a block can't be dropped inside itself
    public bool CanAcceptDropPayload(object payload) =>
        payload is BlockDragPayload blockPayload
        && _slot.Block == null
        && _slot.Accepts(blockPayload.Block)
        && !blockPayload.Block.ContainsSlot(_slot);



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BlockDragPayload blockPayload) return;

        var copy = BlockResolver.DeepCopy(blockPayload.Block);
        if (copy == null) return;

        if (!_slot.TrySetBlock(copy))
        {
            copy.Dispose();
            return;
        }

        blockPayload.RemoveFromSource?.Invoke();
    }



    // model events



    private void OnBlockChanged(BlockModel? _, BlockModel? newBlock) => SetBlockViewModel(newBlock);



    // helpers



    private void SetBlockViewModel(BlockModel? block)
    {
        var oldViewModel = Block;
        Block = block == null ? null : new BlockViewModel(block, () => _slot.RemoveBlock(block));
        oldViewModel?.Dispose();
    }



    private void SubscribeToBlockSlot(BlockSlot slot) => slot.BlockChanged += OnBlockChanged;



    private void UnsubscribeFromBlockSlot(BlockSlot slot) => slot.BlockChanged -= OnBlockChanged;



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromBlockSlot(_slot);
        Block?.Dispose();
        Block = null;
    }
}
