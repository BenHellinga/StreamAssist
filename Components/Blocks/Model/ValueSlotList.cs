using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Model;



// a growing list of text value slots (e.g. Join's parts): there's always one empty slot at the end (typing in it
// or placing a block adds the next), and a slot disappears once it has no text and no block (see GrowingList)
public sealed class ValueSlotList : BlockSlotBase
{
    // private



    private readonly GrowingList<ValueSlot<string>> _items;
    private BlockModel? _owner;



    // public



    public IReadOnlyList<ValueSlot<string>> Items => _items.Items;
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



    public event Action<ValueSlot<string>, int>? ItemInserted
    {
        add => _items.ItemInserted += value;
        remove => _items.ItemInserted -= value;
    }

    public event Action<ValueSlot<string>>? ItemRemoved
    {
        add => _items.ItemRemoved += value;
        remove => _items.ItemRemoved -= value;
    }



    // constructor



    public ValueSlotList(string label) : base(label, typeof(ValueBlock))
    {
        _owner = null;
        _items = new(() => new ValueSlot<string>(""), IsEmpty, SubscribeToItem, UnsubscribeFromItem);
        _items.Changed += RaiseChanged;
    }



    // public api



    // every item's text, top to bottom, run together (spaces aren't added)
    public async Task<string> EvaluateJoinedAsync(TriggerContext context)
    {
        var joined = new StringBuilder();
        foreach (var item in _items.Items.ToArray())
            joined.Append(await item.EvaluateAsync(context));

        return joined.ToString();
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



    private static bool IsEmpty(ValueSlot<string> item) => item.Text == "" && item.BlockSlot.Block == null;



    private void SubscribeToItem(ValueSlot<string> item)
    {
        item.Owner = _owner;
        item.TextChanged += OnItemFilledOrEmptied;
        item.BlockSlot.BlockChanged += OnItemBlockChanged;
        item.Changed += OnItemChanged;
    }



    private void UnsubscribeFromItem(ValueSlot<string> item)
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
        var item = new ValueSlot<string>("");
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
