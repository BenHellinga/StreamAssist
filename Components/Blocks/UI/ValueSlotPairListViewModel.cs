using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one name / value row: the name's slot on the left, the value's on the right (both ValueSlotViews)
public class ValueSlotPairViewModel : ViewModelBase
{
    private readonly ValueSlotPair _pair;

    public ValueSlotViewModel Key { get; }
    public ValueSlotViewModel Value { get; }



    public ValueSlotPairViewModel(ValueSlotPair pair, string keyPlaceholder)
    {
        _pair = pair;
        Key = new(pair.Key, isInList: true, placeholder: keyPlaceholder);
        Value = new(pair.Value, isInList: true);
    }



    public bool RepresentsModel(ValueSlotPair pair) => pair == _pair;



    public override void Dispose()
    {
        Key.Dispose();
        Value.Dispose();
    }
}



// a growing list of name / value rows (e.g. Set Parameters), top to bottom, the last one always empty
public class ValueSlotPairListViewModel : ViewModelBase
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
            _items.Add(new(item, _list.KeyPlaceholder));

        SubscribeToValueSlotPairList(_list);
    }



    // model events



    private void OnItemInserted(ValueSlotPair item, int index) => _items.Insert(index, new(item, _list.KeyPlaceholder));



    private void OnItemRemoved(ValueSlotPair item)
    {
        var viewModel = _items.FirstOrDefault(i => i.RepresentsModel(item));
        if (viewModel == null) return;

        _items.Remove(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private void SubscribeToValueSlotPairList(ValueSlotPairList list)
    {
        list.ItemInserted += OnItemInserted;
        list.ItemRemoved += OnItemRemoved;
    }



    private void UnsubscribeFromValueSlotPairList(ValueSlotPairList list)
    {
        list.ItemInserted -= OnItemInserted;
        list.ItemRemoved -= OnItemRemoved;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromValueSlotPairList(_list);

        foreach (var item in _items)
            item.Dispose();
    }
}
