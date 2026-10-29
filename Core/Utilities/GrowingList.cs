using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Core.Utilities;



// a list that always ends with exactly one empty item: filling it (typing in it, placing something) adds the next
// empty one below, and an item that becomes empty disappears. Used for Match Text rows, redeem rows and Join's parts
//
// the owner says how to make an item, when it counts as empty, and subscribes to the item's events in attach/detach,
// calling Normalize whenever an item may have become empty or filled
public sealed class GrowingList<T> : IDisposable where T : class, IDisposable
{
    // private



    private readonly List<T> _items;
    private readonly Func<T> _create;
    private readonly Func<T, bool> _isEmpty;
    private readonly Action<T> _attach;
    private readonly Action<T> _detach;



    // public



    public IReadOnlyList<T> Items => _items;

    // what's worth saving: the trailing empty item is added back on load
    public IEnumerable<T> NonEmptyItems => _items.Where(item => !_isEmpty(item));



    // events



    public event Action<T, int>? ItemInserted;
    public event Action<T>? ItemRemoved;
    public event Action? Changed; // an item was added or removed



    // constructor



    public GrowingList(Func<T> create, Func<T, bool> isEmpty, Action<T> attach, Action<T> detach)
    {
        _items = [];
        _create = create;
        _isEmpty = isEmpty;
        _attach = attach;
        _detach = detach;

        Normalize();
    }



    // public api



    // removes empty items (except the last) and makes sure the last item is an empty one
    public void Normalize()
    {
        // the item just above the trailing one was emptied (e.g. backspacing a typo): drop the trailing one instead,
        // so the item being edited stays put and keeps its cursor
        while (_items.Count >= 2 && _isEmpty(_items[^1]) && _isEmpty(_items[^2]))
            Remove(_items[^1]);

        for (int i = _items.Count - 2; i >= 0; i--)
            if (_isEmpty(_items[i])) Remove(_items[i]);

        if (_items.Count == 0 || !_isEmpty(_items[^1]))
            Insert(_create(), _items.Count);
    }



    // replaces everything with these (loading a save), empty ones are dropped
    public void Load(IEnumerable<T> items)
    {
        foreach (var item in _items.ToArray())
            Remove(item);

        foreach (var item in items)
        {
            if (_isEmpty(item)) item.Dispose();
            else Insert(item, _items.Count);
        }

        Normalize();
    }



    // helpers



    private void Insert(T item, int index)
    {
        _items.Insert(index, item);
        _attach(item);

        ItemInserted?.Invoke(item, index);
        Changed?.Invoke();
    }



    private void Remove(T item)
    {
        if (!_items.Remove(item)) return;
        _detach(item);

        ItemRemoved?.Invoke(item);
        Changed?.Invoke();
        item.Dispose();
    }



    // disposal



    public void Dispose()
    {
        foreach (var item in _items)
        {
            _detach(item);
            item.Dispose();
        }
    }
}
