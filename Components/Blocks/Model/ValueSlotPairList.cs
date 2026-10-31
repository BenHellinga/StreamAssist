using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Blocks.Model;



// one row of a ValueSlotPairList: a name (a text slot, so it can be typed, a {parameter} or a block) and a value of any
// kind to its right
public sealed class ValueSlotPair : IDisposable
{
    public ValueSlot<string> Key { get; } = new("");
    public AnyValueSlot Value { get; } = new("");

    public bool IsEmpty => Key.Text == "" && Key.BlockSlot.Block == null && Value.Text == "" && Value.BlockSlot.Block == null;

    public void Dispose()
    {
        Key.Dispose();
        Value.Dispose();
    }
}



// how a pair is saved
public class ValueSlotPairData
{
    [JsonPropertyName("key")] public ValueSlotData? Key { get; set; }
    [JsonPropertyName("value")] public ValueSlotData? Value { get; set; }
}



// a growing list of name / value rows (Set Parameters' names, Set Table Row's columns), like Match Text's rows: there's
// always one empty row at the end (typing in it or placing a block adds the next) and a row disappears once both its
// parts are empty (see GrowingList). The name is a text slot, so it can come from a {parameter} or a block too
public sealed class ValueSlotPairList : BlockSlotBase
{
    // private



    private readonly GrowingList<ValueSlotPair> _items;
    private BlockModel? _owner;



    // public



    public string KeyPlaceholder { get; } // what the name box says while it's empty, e.g. "Name…"
    public IReadOnlyList<ValueSlotPair> Items => _items.Items;
    public override IReadOnlyList<BlockModel> Blocks => [];
    public override IReadOnlyList<BlockSlotBase> InnerSlots => _items.Items.SelectMany(p => new BlockSlotBase[] { p.Key, p.Value }).ToList();

    // the rows' errors name the same block
    public override BlockModel? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            foreach (var item in _items.Items) SetOwner(item);
        }
    }



    // events



    public event Action<ValueSlotPair, int>? ItemInserted
    {
        add => _items.ItemInserted += value;
        remove => _items.ItemInserted -= value;
    }

    public event Action<ValueSlotPair>? ItemRemoved
    {
        add => _items.ItemRemoved += value;
        remove => _items.ItemRemoved -= value;
    }



    // constructor



    public ValueSlotPairList(string label, string keyPlaceholder) : base(label, typeof(ValueBlock))
    {
        _owner = null;
        KeyPlaceholder = keyPlaceholder;
        _items = new(() => new ValueSlotPair(), item => item.IsEmpty, SubscribeToItem, UnsubscribeFromItem);
        _items.Changed += RaiseChanged;
    }



    // public api



    // the filled rows, top to bottom: the name (trimmed) and the value with its kind (null for typed text)
    public async Task<List<(string Name, ValueKind? Kind, object Value)>> EvaluateFilledAsync(TriggerContext context)
    {
        var rows = new List<(string, ValueKind?, object)>();
        foreach (var item in _items.Items.Where(i => !i.IsEmpty).ToArray())
        {
            var name = (await item.Key.EvaluateAsync(context)).Trim();
            var (kind, value) = await item.Value.EvaluateRawAsync(context);
            rows.Add((name, kind, value));
        }

        return rows;
    }



    public override void RemoveBlock(BlockModel block)
    {
        foreach (var item in _items.Items.ToArray())
        {
            item.Key.RemoveBlock(block);
            item.Value.RemoveBlock(block);
        }
    }



    // model callbacks



    private void OnItemFilledOrEmptied() => _items.Normalize();



    private void OnItemBlockChanged(BlockModel? _, BlockModel? __) => _items.Normalize();



    private void OnItemChanged() => RaiseChanged();



    // helpers



    private void SetOwner(ValueSlotPair item)
    {
        item.Key.Owner = _owner;
        item.Value.Owner = _owner;
    }



    private void SubscribeToItem(ValueSlotPair item)
    {
        SetOwner(item);
        foreach (var slot in new ValueSlot[] { item.Key, item.Value })
        {
            slot.TextChanged += OnItemFilledOrEmptied;
            slot.BlockSlot.BlockChanged += OnItemBlockChanged;
            slot.Changed += OnItemChanged;
        }
    }



    private void UnsubscribeFromItem(ValueSlotPair item)
    {
        foreach (var slot in new ValueSlot[] { item.Key, item.Value })
        {
            slot.TextChanged -= OnItemFilledOrEmptied;
            slot.BlockSlot.BlockChanged -= OnItemBlockChanged;
            slot.Changed -= OnItemChanged;
        }
    }



    // json (owners save the list through these)



    // the trailing empty row isn't saved, it's added back on load
    public List<ValueSlotPairData> GetData() =>
        _items.NonEmptyItems.Select(i => new ValueSlotPairData { Key = i.Key.GetData(), Value = i.Value.GetData() }).ToList();



    public void LoadData(List<ValueSlotPairData>? data) => _items.Load((data ?? []).Select(itemData =>
    {
        var item = new ValueSlotPair();
        item.Key.LoadData(itemData.Key);
        item.Value.LoadData(itemData.Value);
        return item;
    }));



    // disposal



    public override void Dispose()
    {
        _items.Changed -= RaiseChanged;
        _items.Dispose();
    }
}
