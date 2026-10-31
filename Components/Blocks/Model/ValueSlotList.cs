using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Model;



// a growing list of value slots of one kind (e.g. Operation's numbers, Pick Random's texts): there's always one empty
// slot at the end (typing in it or placing a block adds the next), and a slot disappears once it has no text and no
// block (see GrowingList)
public sealed class ValueSlotList : BlockSlotBase
{
    // private



    private readonly ValueKind _kind;
    private readonly GrowingList<ValueSlot> _items;
    private BlockModel? _owner;



    // public



    public IReadOnlyList<ValueSlot> Items => _items.Items;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _items.Items;

    // the items' errors name the same block
    public override BlockModel? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            foreach (var item in _items.Items) item.Owner = value;
        }
    }



    // events



    public event Action<ValueSlot, int>? ItemInserted
    {
        add => _items.ItemInserted += value;
        remove => _items.ItemInserted -= value;
    }

    public event Action<ValueSlot>? ItemRemoved
    {
        add => _items.ItemRemoved += value;
        remove => _items.ItemRemoved -= value;
    }



    // constructor



    public ValueSlotList(string label, ValueKind kind = ValueKind.Text) : base(label, typeof(ValueBlock))
    {
        _owner = null;
        _kind = kind;
        _items = new(CreateItem, IsEmpty, SubscribeToItem, UnsubscribeFromItem);
        _items.Changed += RaiseChanged;
    }



    // public api



    // the filled items' values, top to bottom (the empty one at the end isn't asked, it would fail with "nothing entered")
    public async Task<List<T>> EvaluateFilledAsync<T>(TriggerContext context) where T : notnull
    {
        var values = new List<T>();
        foreach (var item in _items.Items.Where(item => !IsEmpty(item)).OfType<ValueSlot<T>>().ToArray())
            values.Add(await item.EvaluateAsync(context));

        return values;
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var item in _items.Items.ToArray())
            item.RemoveBlock(block);
    }



    // model callbacks



    private void OnItemFilledOrEmptied() => _items.Normalize();



    private void OnItemBlockChanged(BlockModel? _, BlockModel? __) => _items.Normalize();



    private void OnItemChanged() => RaiseChanged();



    // helpers



    private ValueSlot CreateItem() => _kind switch
    {
        ValueKind.Int => new ValueSlot<int>(""),
        ValueKind.Double => new ValueSlot<double>(""),
        ValueKind.Bool => new ValueSlot<bool>(""),
        ValueKind.Char => new ValueSlot<char>(""),
        _ => new ValueSlot<string>(""),
    };



    private static bool IsEmpty(ValueSlot item) => item.Text == "" && item.BlockSlot.Block == null;



    private void SubscribeToItem(ValueSlot item)
    {
        item.Owner = _owner;
        item.TextChanged += OnItemFilledOrEmptied;
        item.BlockSlot.BlockChanged += OnItemBlockChanged;
        item.Changed += OnItemChanged;
    }



    private void UnsubscribeFromItem(ValueSlot item)
    {
        item.TextChanged -= OnItemFilledOrEmptied;
        item.BlockSlot.BlockChanged -= OnItemBlockChanged;
        item.Changed -= OnItemChanged;
    }



    // json (owners save the list through these)



    // the trailing empty item isn't saved, it's added back on load
    public List<ValueSlotData> GetData() => _items.NonEmptyItems.Select(i => i.GetData()).ToList();



    public void LoadData(List<ValueSlotData>? data) => _items.Load((data ?? []).Select(itemData =>
    {
        var item = CreateItem();
        item.LoadData(itemData);
        return item;
    }));



    // disposal



    public override void Dispose()
    {
        _items.Changed -= RaiseChanged;
        _items.Dispose();
    }
}
