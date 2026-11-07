using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// view model for a value slot: a text box to type the value in (its placeholder says the type, e.g. "+ int"),
// or the card of the value block placed in it, dropping a block on the text box places it
public partial class ValueSlotViewModel : ViewModelBase, IDroppable, ISlotTagSource
{
    // private



    private readonly ValueSlot _slot;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNewEntry))] private string _text;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNewEntry))] private BlockViewModel? _block;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasConnector))] private string _connector; // e.g. "==" above Equals' second value
    private bool _isInList; // one of a growing list's slots
    private readonly string? _placeholder; // instead of the type, e.g. a name box's "Name…"
    private readonly IDroppable? _list; // the growing list this slot is in, which reorders its items (see PassesOn)



    // public



    public BlockSlotBase TagSlot => _slot; // clicked while empty: the lists show what fits
    public string Label => _slot.Label + ":";
    public bool HasLabel => !string.IsNullOrEmpty(_slot.Label);
    public bool HasConnector => Connector != "";
    public string Placeholder => _placeholder ?? ValueTypes.Placeholder(_slot.Kind);
    public bool IsText => _slot.Kind is ValueKind.Text or null && !IsMenu; // text can be several lines
    public bool IsMenu => _slot is MenuSlot; // a dropdown instead of a text box

    // a dropdown's options, listed when the slot is shown (a saved option that isn't there now, e.g. an unplugged
    // device, is kept in the list so it still shows and isn't lost)
    public IReadOnlyList<string> Options { get; }

    // the empty slot at the end of a growing list (typing in it adds the next): shown with a dim border
    public bool IsNewEntry => _isInList && string.IsNullOrEmpty(Text) && Block == null;



    // constructor



    public ValueSlotViewModel(ValueSlot slot, bool isInList = false, string? placeholder = null, IDroppable? list = null)
    {
        _slot = slot;
        _isInList = isInList;
        _placeholder = placeholder;
        _list = list;
        _text = _slot.Text;
        _block = null;
        _connector = _slot.Connector;
        Options = _slot is MenuSlot menu ? WithCurrent(menu.Options, _slot.Text) : [];

        SetBlockViewModel(_slot.BlockSlot.Block);
        SubscribeToValueSlot(_slot);
    }



    // public api



    public bool RepresentsModel(ValueSlot slot) => slot == _slot;



    // ui events



    // typing (or picking) goes straight to the model; a dropdown clearing itself (its list changed) isn't a pick
    partial void OnTextChanged(string value)
    {
        if (value == null && IsMenu) return;
        _slot.SetText(value ?? "");
    }



    // drag drop



    // a value block of a kind that fits (the slot decides, see ValueTypes), never into itself
    public bool CanAcceptDropPayload(object payload) =>
        payload is BlockDragPayload blockPayload
        && _slot.BlockSlot.Block == null
        && _slot.BlockSlot.Accepts(blockPayload.Block)
        && !blockPayload.Block.ContainsSlot(_slot);



    // in a growing list, a block from another of its items is the list's to handle: it moves that item before this one
    public bool PassesOn(object payload) => _list?.CanAcceptDropPayload(payload) == true;



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



    private void OnConnectorChanged() => Connector = _slot.Connector;



    private void OnBlockChanged(BlockModel? _, BlockModel? newBlock) => SetBlockViewModel(newBlock);



    // helpers



    private static List<string> WithCurrent(List<string> options, string current) =>
        current == "" || options.Contains(current) ? options : [.. options, current];



    private void SetBlockViewModel(BlockModel? block)
    {
        var oldViewModel = Block;
        Block = block == null ? null : new BlockViewModel(block, () => _slot.BlockSlot.RemoveBlock(block));
        oldViewModel?.Dispose();
    }



    private void SubscribeToValueSlot(ValueSlot slot)
    {
        slot.TextChanged += OnSlotTextChanged;
        slot.ConnectorChanged += OnConnectorChanged;
        slot.BlockSlot.BlockChanged += OnBlockChanged;
    }



    private void UnsubscribeFromValueSlot(ValueSlot slot)
    {
        slot.TextChanged -= OnSlotTextChanged;
        slot.ConnectorChanged -= OnConnectorChanged;
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
