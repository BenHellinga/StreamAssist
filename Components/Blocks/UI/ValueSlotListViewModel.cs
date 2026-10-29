using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// a growing list of value slots (e.g. Join's parts), one ValueSlotView each, top to bottom
public class ValueSlotListViewModel : ViewModelBase
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
            _items.Add(new(item));

        SubscribeToValueSlotList(_list);
    }



    // model events



    private void OnItemInserted(ValueSlot<string> item, int index) => _items.Insert(index, new(item));



    private void OnItemRemoved(ValueSlot<string> item)
    {
        var viewModel = _items.FirstOrDefault(i => i.RepresentsModel(item));
        if (viewModel == null) return;

        _items.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private void SubscribeToValueSlotList(ValueSlotList list)
    {
        list.ItemInserted += OnItemInserted;
        list.ItemRemoved += OnItemRemoved;
    }



    private void UnsubscribeFromValueSlotList(ValueSlotList list)
    {
        list.ItemInserted -= OnItemInserted;
        list.ItemRemoved -= OnItemRemoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromValueSlotList(_list);

        foreach (var item in _items)
            item.Dispose();
    }
}
