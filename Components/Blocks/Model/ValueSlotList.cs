using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamAssist.Components.Blocks.Model;



// a growing list of text value slots (e.g. Join's parts): there's always one empty slot at the end (typing in it
// or placing a block adds the next), and a slot disappears once it has no text and no block
public sealed class ValueSlotList : BlockSlotBase
{
    // private



    private readonly List<ValueSlot<string>> _items;
    private BlockModel? _owner;



    // public



    public IReadOnlyList<ValueSlot<string>> Items => _items;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _items;

    // the items' errors name the same block
    public override BlockModel? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            foreach (var item in _items) item.Owner = value;
        }
    }



    // events



    public event Action<ValueSlot<string>, int>? ItemInserted;
    public event Action<ValueSlot<string>>? ItemRemoved;



    // constructor



    public ValueSlotList(string label) : base(label, typeof(ValueBlock))
    {
        _items = [];
        _owner = null;
        Normalize();
    }



    // public api



    // every item's text, top to bottom, run together (spaces aren't added)
    public async Task<string> EvaluateJoinedAsync(TriggerContext context)
    {
        var joined = new StringBuilder();
        foreach (var item in _items.ToArray())
            joined.Append(await item.EvaluateAsync(context));

        return joined.ToString();
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var item in _items.ToArray())
            item.RemoveBlock(block);
    }



    // item management



    private static bool IsEmpty(ValueSlot<string> item) => item.Text == "" && item.BlockSlot.Block == null;



    // removes empty items (except the last) and makes sure the last item is an empty one
    private void Normalize()
    {
        // the item above the trailing one was emptied: drop the trailing one instead, so the edited one keeps its cursor
        while (_items.Count >= 2 && IsEmpty(_items[^1]) && IsEmpty(_items[^2]))
            RemoveItem(_items[^1]);

        for (int i = _items.Count - 2; i >= 0; i--)
            if (IsEmpty(_items[i])) RemoveItem(_items[i]);

        if (_items.Count == 0 || !IsEmpty(_items[^1]))
            InsertItem(new ValueSlot<string>(""), _items.Count);
    }



    private void InsertItem(ValueSlot<string> item, int index)
    {
        item.Owner = _owner;
        _items.Insert(index, item);
        SubscribeToItem(item);

        ItemInserted?.Invoke(item, index);
        RaiseChanged();
    }



    private void RemoveItem(ValueSlot<string> item)
    {
        if (!_items.Remove(item)) return;
        UnsubscribeFromItem(item);

        ItemRemoved?.Invoke(item);
        RaiseChanged();
        item.Dispose();
    }



    // model callbacks



    private void OnItemTextChanged() => Normalize();



    private void OnItemBlockChanged(BlockModel? _, BlockModel? __) => Normalize();



    private void OnItemChanged() => RaiseChanged();



    // helpers



    private void SubscribeToItem(ValueSlot<string> item)
    {
        item.TextChanged += OnItemTextChanged;
        item.BlockSlot.BlockChanged += OnItemBlockChanged;
        item.Changed += OnItemChanged;
    }



    private void UnsubscribeFromItem(ValueSlot<string> item)
    {
        item.TextChanged -= OnItemTextChanged;
        item.BlockSlot.BlockChanged -= OnItemBlockChanged;
        item.Changed -= OnItemChanged;
    }



    // json (owners save the list through these)



    // the trailing empty item isn't saved, it's added back on load
    public List<ValueSlotData> GetData() => _items.Where(i => !IsEmpty(i)).Select(i => i.GetData()).ToList();



    public void LoadData(List<ValueSlotData>? data)
    {
        foreach (var item in _items.ToArray())
            RemoveItem(item);

        foreach (var itemData in data ?? [])
        {
            var item = new ValueSlot<string>("");
            item.LoadData(itemData);

            if (IsEmpty(item)) item.Dispose();
            else InsertItem(item, _items.Count);
        }

        Normalize();
    }



    // disposal



    public override void Dispose()
    {
        foreach (var item in _items)
        {
            UnsubscribeFromItem(item);
            item.Dispose();
        }
    }
}
