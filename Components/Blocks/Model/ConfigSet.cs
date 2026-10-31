using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// one named config of a tab's bindings (the whole chat tree, the alerts and redeems, every keyboard's keys)
public interface INamedConfig : IDisposable
{
    string Name { get; }
    void SetName(string name); // only ConfigSet names configs (a copy gets its new name)
    event Action? Changed; // anything in it changed (saved)
}



// what the config bar (Blocks/UI/ConfigBar) needs, whatever the configs hold
public interface IConfigSet
{
    IReadOnlyList<string> Names { get; }
    string? SelectedName { get; } // null while there are no configs
    object? SelectedItem { get; } // the config itself, to tell a new one from an old one with the same name
    bool TrySelect(string name);
    bool TryCreate(string name); // a copy of the selected one (the first one starts empty)
    bool TryDelete(string name);
    event Action? ListChanged; // a config was added, removed, or another one selected
}



// a tab's configs and the one in use (e.g. a normal chat tree and an April Fools one): switching swaps everything the
// tab runs at once. A tab starts with none, and while it has none nothing can be bound and nothing runs; the first one
// starts empty, later ones as a copy of the selected one, and deleting the last goes back to none. Names are unique
// (case ignored). Everything goes through the Try methods, so a block or chat command could switch configs too
public sealed class ConfigSet<T> : IConfigSet, IDisposable where T : class, INamedConfig
{
    // constants



    // private



    private readonly string _what; // for errors, e.g. "chat config"
    private readonly Func<T> _createEmpty;
    private readonly List<T> _items;
    private T? _selected;
    private string? _pendingSelected; // the saved selection, found once the configs are loaded



    // public



    public IReadOnlyList<T> Items => _items;
    public T? Selected => _selected; // null while there are no configs
    public IReadOnlyList<string> Names => _items.Select(i => i.Name).ToList();
    public string? SelectedName => _selected?.Name;
    public object? SelectedItem => _selected;



    // events



    public event Action<T?, T?>? SelectedChanged; // old, new (null: no config)
    public event Action<T>? Added;
    public event Action<T>? Removed; // after it's taken out, before it's disposed
    public event Action? ListChanged;
    public event Action? Changed; // anything to save: a config's contents, the list or the selection



    // constructor



    public ConfigSet(string what, Func<T> createEmpty)
    {
        _what = what;
        _createEmpty = createEmpty;
        _items = [];
        _selected = null;
        _pendingSelected = null;
    }



    // public api



    public T? Find(string name) => _items.FirstOrDefault(i => string.Equals(i.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));



    public bool TrySelect(string name)
    {
        var found = Find(name);
        if (found == null)
        {
            AppLog.WriteError($"can't switch to {_what} '{name.Trim()}', not found");
            return false;
        }

        Select(found);
        return true;
    }



    // a copy of the selected config (an empty one if there's none yet), named name, and switched to
    public bool TryCreate(string name)
    {
        var trimmed = name.Trim();
        if (trimmed == "")
        {
            AppLog.WriteError($"{_what} needs a name");
            return false;
        }

        if (Find(trimmed) != null)
        {
            AppLog.WriteError($"{_what} '{trimmed}' already exists");
            return false;
        }

        var copy = _selected == null ? _createEmpty() : Copy(_selected);
        if (copy == null)
        {
            AppLog.WriteError($"can't copy {_what} '{_selected!.Name}'");
            return false;
        }

        copy.SetName(trimmed);
        _items.Add(copy);
        copy.Changed += OnItemChanged;
        Added?.Invoke(copy);
        Select(copy);
        ListChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }



    // deleting the selected one switches to the one before it, deleting the last one leaves the tab with none
    public bool TryDelete(string name)
    {
        var found = Find(name);
        if (found == null)
        {
            AppLog.WriteError($"can't delete {_what} '{name.Trim()}', not found");
            return false;
        }

        if (found == _selected)
        {
            var index = _items.IndexOf(found);
            Select(_items.Count == 1 ? null : _items[index > 0 ? index - 1 : 1]);
        }

        _items.Remove(found);
        found.Changed -= OnItemChanged;
        Removed?.Invoke(found);
        found.Dispose();
        ListChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }



    // json (owners save the set through these)



    // the loaded configs replace any there were, names made unique
    public void LoadItems(List<T>? items)
    {
        foreach (var item in _items)
        {
            item.Changed -= OnItemChanged;
            item.Dispose();
        }

        _items.Clear();
        foreach (var item in items ?? [])
        {
            if (item.Name.Trim() == "" || Find(item.Name) != null)
            {
                var n = _items.Count + 1;
                while (Find($"{_what} {n}") != null) n++;
                AppLog.WriteWarning($"{_what} '{item.Name}' renamed '{_what} {n}' (name missing or used twice)");
                item.SetName($"{_what} {n}");
            }

            _items.Add(item);
            item.Changed += OnItemChanged;
        }

        _selected = _items.FirstOrDefault();
        ResolveSelected();
    }



    public void LoadSelected(string? name)
    {
        _pendingSelected = name;
        ResolveSelected();
    }



    // helpers



    private void Select(T? item)
    {
        if (item == _selected) return;

        var old = _selected;
        _selected = item;
        SelectedChanged?.Invoke(old, item);
        ListChanged?.Invoke();
        Changed?.Invoke();
    }



    // json is read in any order, so the saved selection is looked for whenever either part arrives
    private void ResolveSelected()
    {
        if (_pendingSelected == null) return;

        var found = Find(_pendingSelected);
        if (found != null) _selected = found;
    }



    // through json, the same way it's saved (so the copy is exactly what loading the saved file would give)
    private static T? Copy(T item)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(item));
        }
        catch (Exception)
        {
            return null;
        }
    }



    private void OnItemChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        foreach (var item in _items)
        {
            item.Changed -= OnItemChanged;
            item.Dispose();
        }
    }
}
