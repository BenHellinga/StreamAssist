using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// a growing list of value slots (e.g. Join's parts), one ValueSlotView each, top to bottom. Dragging an item's block
// onto another item moves the whole item before that one (a line lights up above it), so they can be reordered
public class ValueSlotListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly ValueSlotList _list;
    private readonly ObservableCollection<ValueSlotViewModel> _items;



    // public



    public ObservableCollection<ValueSlotViewModel> Items => _items;



    // constructor



    public ValueSlotListViewModel(ValueSlotList list)
    {
        _list = list;
        _items = [];

        foreach (var item in _list.Items)
            _items.Add(new(item, isInList: true, list: this));

        SubscribeToValueSlotList(_list);
    }



    // drag drop



    // only reordering: a block that sits in one of this list's items
    public bool CanAcceptDropPayload(object payload) => FindItem(payload) != null;



    // anything else goes on to the target around this list (e.g. inserting before the block it's in)
    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the item it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindItem(payload) is not { } item) return;
        if (zoneContext is not ValueSlotViewModel before) return;

        _list.TryMoveItem(item, _items.IndexOf(before));
    }



    // model events



    private void OnItemMoved(ValueSlot _, int from, int to) => _items.Move(from, to);



    private void OnItemInserted(ValueSlot item, int index) => _items.Insert(index, new(item, isInList: true, list: this));



    private void OnItemRemoved(ValueSlot item)
    {
        var viewModel = _items.FirstOrDefault(i => i.RepresentsModel(item));
        if (viewModel == null) return;

        _items.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private ValueSlot? FindItem(object payload) =>
        payload is BlockDragPayload blockPayload ? _list.Items.FirstOrDefault(i => i.BlockSlot.Block == blockPayload.Block) : null;



    private void SubscribeToValueSlotList(ValueSlotList list)
    {
        list.ItemInserted += OnItemInserted;
        list.ItemRemoved += OnItemRemoved;
        list.ItemMoved += OnItemMoved;
    }



    private void UnsubscribeFromValueSlotList(ValueSlotList list)
    {
        list.ItemInserted -= OnItemInserted;
        list.ItemRemoved -= OnItemRemoved;
        list.ItemMoved -= OnItemMoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromValueSlotList(_list);

        foreach (var item in _items)
            item.Dispose();
    }
}
