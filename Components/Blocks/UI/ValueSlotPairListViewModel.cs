using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one name / value row: the name's slot on the left, the value's on the right (both ValueSlotViews)
public class ValueSlotPairViewModel : ViewModelBase
{
    private readonly ValueSlotPair _pair;

    public ValueSlotViewModel Key { get; }
    public ValueSlotViewModel Value { get; }



    public ValueSlotPairViewModel(ValueSlotPair pair, string keyPlaceholder, IDroppable list)
    {
        _pair = pair;
        Key = new(pair.Key, isInList: true, placeholder: keyPlaceholder, list: list);
        Value = new(pair.Value, isInList: true, list: list);
    }



    public bool RepresentsModel(ValueSlotPair pair) => pair == _pair;



    public override void Dispose()
    {
        Key.Dispose();
        Value.Dispose();
    }
}



// a growing list of name / value rows (e.g. Set Parameters), top to bottom, the last one always empty. Dragging a
// row's block (name or value) onto another row moves the whole row before that one, so they can be reordered
public class ValueSlotPairListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly ValueSlotPairList _list;
    private readonly ObservableCollection<ValueSlotPairViewModel> _items;



    // public



    public ObservableCollection<ValueSlotPairViewModel> Items => _items;



    // constructor



    public ValueSlotPairListViewModel(ValueSlotPairList list)
    {
        _list = list;
        _items = [];

        foreach (var item in _list.Items)
            _items.Add(new(item, _list.KeyPlaceholder, this));

        SubscribeToValueSlotPairList(_list);
    }



    // drag drop



    // only reordering: a block that sits in one of this list's rows
    public bool CanAcceptDropPayload(object payload) => FindItem(payload) != null;



    // anything else goes on to the target around this list (e.g. inserting before the block it's in)
    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindItem(payload) is not { } item) return;
        if (zoneContext is not ValueSlotPairViewModel before) return;

        _list.TryMoveItem(item, _items.IndexOf(before));
    }



    // model events



    private void OnItemMoved(ValueSlotPair _, int from, int to) => _items.Move(from, to);



    private void OnItemInserted(ValueSlotPair item, int index) => _items.Insert(index, new(item, _list.KeyPlaceholder, this));



    private void OnItemRemoved(ValueSlotPair item)
    {
        var viewModel = _items.FirstOrDefault(i => i.RepresentsModel(item));
        if (viewModel == null) return;

        _items.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private ValueSlotPair? FindItem(object payload) =>
        payload is BlockDragPayload blockPayload
            ? _list.Items.FirstOrDefault(i => i.Key.BlockSlot.Block == blockPayload.Block || i.Value.BlockSlot.Block == blockPayload.Block)
            : null;



    private void SubscribeToValueSlotPairList(ValueSlotPairList list)
    {
        list.ItemInserted += OnItemInserted;
        list.ItemRemoved += OnItemRemoved;
        list.ItemMoved += OnItemMoved;
    }



    private void UnsubscribeFromValueSlotPairList(ValueSlotPairList list)
    {
        list.ItemInserted -= OnItemInserted;
        list.ItemRemoved -= OnItemRemoved;
        list.ItemMoved -= OnItemMoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromValueSlotPairList(_list);

        foreach (var item in _items)
            item.Dispose();
    }
}
