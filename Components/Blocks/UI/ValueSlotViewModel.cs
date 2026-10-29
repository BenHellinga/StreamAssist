using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for a value slot: a text box to type the value in (its placeholder says the type, e.g. "+ int"),
// or the card of the value block placed in it, dropping a block on the text box places it
public partial class ValueSlotViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly ValueSlot _slot;

    [ObservableProperty] private string _text;
    [ObservableProperty] private BlockViewModel? _block;



    // public



    public string Label => _slot.Label + ":";
    public bool HasLabel => !string.IsNullOrEmpty(_slot.Label);
    public string Placeholder => ValueTypes.Placeholder(_slot.Kind);
    public bool IsText => _slot.Kind is ValueKind.Text or null; // text can be several lines



    // constructor



    public ValueSlotViewModel(ValueSlot slot)
    {
        _slot = slot;
        _text = _slot.Text;
        _block = null;

        SetBlockViewModel(_slot.BlockSlot.Block);
        SubscribeToValueSlot(_slot);
    }



    // public api



    public bool RepresentsModel(ValueSlot slot) => slot == _slot;



    // ui events



    // typing goes straight to the model
    partial void OnTextChanged(string value) => _slot.SetText(value ?? "");



    // drag drop



    // a value block of a kind that fits (the slot decides, see ValueTypes), never into itself
    public bool CanAcceptDropPayload(object payload) =>
        payload is BlockDragPayload blockPayload
        && _slot.BlockSlot.Block == null
        && _slot.BlockSlot.Accepts(blockPayload.Block)
        && !blockPayload.Block.ContainsSlot(_slot);



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BlockDragPayload blockPayload) return;

        var copy = BlockResolver.DeepCopy(blockPayload.Block);
        if (copy == null) return;

        if (!_slot.BlockSlot.TrySetBlock(copy))
        {
            copy.Dispose();
            return;
        }

        blockPayload.RemoveFromSource?.Invoke();
    }



    // model events



    private void OnSlotTextChanged() => Text = _slot.Text;



    private void OnBlockChanged(BlockModel? _, BlockModel? newBlock) => SetBlockViewModel(newBlock);



    // helpers



    private void SetBlockViewModel(BlockModel? block)
    {
        var oldViewModel = Block;
        Block = block == null ? null : new BlockViewModel(block, () => _slot.BlockSlot.RemoveBlock(block));
        oldViewModel?.Dispose();
    }



    private void SubscribeToValueSlot(ValueSlot slot)
    {
        slot.TextChanged += OnSlotTextChanged;
        slot.BlockSlot.BlockChanged += OnBlockChanged;
    }



    private void UnsubscribeFromValueSlot(ValueSlot slot)
    {
        slot.TextChanged -= OnSlotTextChanged;
        slot.BlockSlot.BlockChanged -= OnBlockChanged;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromValueSlot(_slot);
        Block?.Dispose();
        Block = null;
    }
}
