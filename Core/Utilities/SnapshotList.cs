using System;
using System.Collections;
using System.Collections.Generic;

namespace StreamAssist.Core.Utilities;



// a list that's changed on one thread (the UI) and safely read from any other (runs on background threads): every
// change makes a new array, so a reader looping over it keeps the one it started with and never sees it half-changed
// (copy-on-write, fine for the short lists blocks have)
public sealed class SnapshotList<T> : IReadOnlyList<T>
{
    // private



    private T[] _items = [];
    private readonly object _changing = new();



    // public



    public int Count => _items.Length;

    public T this[int index] => _items[index];

    // the list as it is now (never changes afterwards)
    public IReadOnlyList<T> Snapshot => _items;



    // changes



    public void Insert(int index, T item) => Change(list => list.Insert(index, item));



    public void Add(T item) => Change(list => list.Add(item));



    public bool Remove(T item)
    {
        var removed = false;
        Change(list => removed = list.Remove(item));
        return removed;
    }



    public void RemoveAt(int index) => Change(list => list.RemoveAt(index));



    public void Clear() => Change(list => list.Clear());



    public int IndexOf(T item) => Array.IndexOf(_items, item);



    // helpers



    private void Change(Action<List<T>> change)
    {
        lock (_changing)
        {
            var list = new List<T>(_items);
            change(list);
            _items = list.ToArray();
        }
    }



    // enumerating



    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
