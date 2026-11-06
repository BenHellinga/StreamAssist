using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Dashboard.Model;



// one config of the Dashboard tab: its docks and how they're laid out (a tree of splits, see DockLayoutNode). Every dock
// is somewhere in the layout: a new one goes beside the dock it's dropped on, removing one closes up the gap. Names are
// unique (case ignored), blocks find docks by name
public sealed class DashboardConfigModel : INamedConfig, IJsonOnDeserialized
{
    // private



    private string _name;
    private readonly List<DockModel> _docks;
    private DockLayoutNode? _layout;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyList<DockModel> Docks => _docks; // in the order they were added (the dock list)
    [JsonIgnore] public DockLayoutNode? Layout => _layout; // null with no docks



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("docks")]
    private List<DockModel> JsonDocks
    {
        get => _docks;
        set
        {
            _docks.Clear();
            _docks.AddRange(value);
        }
    }

    [JsonInclude] [JsonPropertyName("layout")]
    private DockLayoutNode? JsonLayout
    {
        get => _layout;
        set => _layout = value;
    }



    // events



    public event Action? Changed; // anything saved changed
    public event Action? DocksChanged; // a dock was added or removed
    public event Action? LayoutChanged; // the regions changed (not just their sizes)



    // constructor



    public DashboardConfigModel()
    {
        _name = "";
        _docks = [];
        _layout = null;
    }



    // loading: the layout is made to match the docks (a dock missing from it goes on the right, a region whose dock
    // is gone closes up), and names made unique
    public void OnDeserialized()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dock in _docks)
        {
            if (dock.Name.Trim() != "" && names.Add(dock.Name)) continue;

            var n = 1;
            while (!names.Add($"Dock {n}")) n++;
            AppLog.WriteWarning($"dock '{dock.Name}' renamed 'Dock {n}' (name missing or used twice)");
            dock.SetName($"Dock {n}");
        }

        var ids = _docks.Select(d => d.Id).ToHashSet();
        _layout = _layout == null ? null : Tidy(_layout, ids);
        foreach (var dock in _docks.Where(d => !Contains(_layout, d.Id)))
            Place(dock);

        foreach (var dock in _docks) dock.Changed += OnDockChanged;
    }



    // public api



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        Changed?.Invoke();
    }



    public DockModel? FindDock(string name) =>
        _docks.FirstOrDefault(d => string.Equals(d.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));



    // a new dock on one side of another (the dock list's cards are dropped there), or on the right of everything
    public bool TryAddDock(string name, DockKind kind, DockModel? target = null, DockEdge edge = DockEdge.Right)
    {
        var trimmed = name.Trim();
        if (!IsFreeName(trimmed, null)) return false;

        if (target != null && !_docks.Contains(target))
        {
            AppLog.WriteError($"can't add dock beside '{target.Name}', not found");
            return false;
        }

        var dock = DockModel.Create(trimmed, kind);
        _docks.Add(dock);
        dock.Changed += OnDockChanged;
        if (target == null) Place(dock);
        else _layout = Insert(_layout!, dock.Id, target.Id, edge);

        DocksChanged?.Invoke();
        LayoutChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }



    // the same, named after its kind ("Console", then "Console 2", ..., "Timers", "Buttons"), renamed in its header
    public bool TryAddDock(DockKind kind, DockModel? target = null, DockEdge edge = DockEdge.Right) =>
        TryAddDock(NewDockName(kind), kind, target, edge);



    public string NewDockName(DockKind kind)
    {
        var title = DockModel.KindTitle(kind);
        var name = title;
        for (var n = 2; FindDock(name) != null; n++) name = $"{title} {n}";
        return name;
    }



    public bool TryRenameDock(DockModel dock, string name)
    {
        var trimmed = name.Trim();
        if (!_docks.Contains(dock))
        {
            AppLog.WriteError($"can't rename dock '{dock.Name}', not found");
            return false;
        }

        if (trimmed == dock.Name) return true;
        if (!IsFreeName(trimmed, dock)) return false;

        dock.SetName(trimmed);
        return true;
    }



    // the dock goes, and its region closes up (the neighbours share its space)
    public bool TryRemoveDock(DockModel dock)
    {
        if (!_docks.Contains(dock))
        {
            AppLog.WriteError($"can't remove dock '{dock.Name}', not found");
            return false;
        }

        _docks.Remove(dock);
        dock.Changed -= OnDockChanged;
        _layout = Remove(_layout, dock.Id);

        DocksChanged?.Invoke();
        LayoutChanged?.Invoke();
        Changed?.Invoke();
        dock.Dispose(); // after the views let go of it
        return true;
    }



    // takes the dock out of its region and puts it on one side of another dock: beside it in the same row/column if
    // that already runs the right way, otherwise the target's region splits in two. Onto itself does nothing
    public bool TryMoveDock(DockModel dock, DockModel target, DockEdge edge)
    {
        if (!_docks.Contains(dock) || !_docks.Contains(target))
        {
            AppLog.WriteError($"can't move dock '{dock.Name}', not found");
            return false;
        }

        if (dock == target) return true;

        _layout = Remove(_layout, dock.Id);
        _layout = Insert(_layout!, dock.Id, target.Id, edge);

        LayoutChanged?.Invoke();
        Changed?.Invoke();
        return true;
    }



    // a splitter was dragged: the split's shares (one per region, saved, the layout doesn't change shape)
    public void SetSizes(DockLayoutNode split, IReadOnlyList<double> sizes)
    {
        if (split.IsDock || sizes.Count != split.Children.Count || sizes.Any(s => !(s > 0))) return;
        if (split.Sizes.SequenceEqual(sizes)) return;

        split.Sizes = [.. sizes];
        Changed?.Invoke();
    }



    // layout helpers



    private void Place(DockModel dock)
    {
        var leaf = DockLayoutNode.ForDock(dock.Id);

        if (_layout == null) _layout = leaf;
        else if (!_layout.IsDock && _layout.Direction == SplitDirection.Horizontal)
        {
            _layout.Children.Add(leaf);
            _layout.Sizes.Add(_layout.Sizes.Average());
        }
        else _layout = DockLayoutNode.Split(SplitDirection.Horizontal, _layout, leaf);
    }



    // the layout without that dock's region (null if it was the only one)
    private static DockLayoutNode? Remove(DockLayoutNode? node, string dockId)
    {
        if (node == null || node.DockId == dockId) return null;
        if (node.IsDock) return node;

        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = Remove(node.Children[i], dockId);
            if (child == null)
            {
                node.Children.RemoveAt(i);
                node.Sizes.RemoveAt(i);
            }
            else node.Children[i] = child;
        }

        return Collapse(node);
    }



    private static DockLayoutNode Insert(DockLayoutNode root, string dockId, string targetId, DockEdge edge)
    {
        var direction = edge is DockEdge.Left or DockEdge.Right ? SplitDirection.Horizontal : SplitDirection.Vertical;
        var after = edge is DockEdge.Right or DockEdge.Bottom;
        var leaf = DockLayoutNode.ForDock(dockId);

        if (root.DockId == targetId)
            return after ? DockLayoutNode.Split(direction, root, leaf) : DockLayoutNode.Split(direction, leaf, root);

        var parent = FindParent(root, targetId)!;
        var index = parent.Children.FindIndex(c => c.DockId == targetId);

        // the row/column already runs this way: the new region takes half of the target's share, beside it
        if (parent.Direction == direction)
        {
            var half = parent.Sizes[index] / 2;
            parent.Sizes[index] = half;
            parent.Children.Insert(after ? index + 1 : index, leaf);
            parent.Sizes.Insert(after ? index + 1 : index, half);
            return root;
        }

        var target = parent.Children[index];
        parent.Children[index] = after ? DockLayoutNode.Split(direction, target, leaf) : DockLayoutNode.Split(direction, leaf, target);
        return root;
    }



    // a split left with one region becomes that region, and a split inside one running the same way merges into it
    // (its regions keep their share of its space)
    private static DockLayoutNode Collapse(DockLayoutNode node)
    {
        if (node.IsDock) return node;
        if (node.Children.Count == 1) return node.Children[0];

        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (child.IsDock || child.Direction != node.Direction) continue;

            var share = node.Sizes[i];
            var total = child.Sizes.Sum();
            node.Children.RemoveAt(i);
            node.Sizes.RemoveAt(i);
            node.Children.InsertRange(i, child.Children);
            node.Sizes.InsertRange(i, child.Sizes.Select(s => share * s / total));
        }

        return node;
    }



    // a loaded layout: regions of unknown (or repeated) docks dropped, broken sizes reset, then tidied
    private static DockLayoutNode? Tidy(DockLayoutNode node, HashSet<string> ids)
    {
        if (node.IsDock) return ids.Remove(node.DockId!) ? node : null;

        if (node.Sizes.Count != node.Children.Count || node.Sizes.Any(s => !(s > 0)))
            node.Sizes = node.Children.Select(_ => 1.0).ToList();

        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = Tidy(node.Children[i], ids);
            if (child == null)
            {
                node.Children.RemoveAt(i);
                node.Sizes.RemoveAt(i);
            }
            else node.Children[i] = child;
        }

        return node.Children.Count == 0 ? null : Collapse(node);
    }



    private static DockLayoutNode? FindParent(DockLayoutNode node, string dockId)
    {
        if (node.IsDock) return null;

        foreach (var child in node.Children)
        {
            if (child.DockId == dockId) return node;
            if (FindParent(child, dockId) is { } found) return found;
        }

        return null;
    }



    private static bool Contains(DockLayoutNode? node, string dockId) =>
        node != null && (node.DockId == dockId || node.Children.Any(c => Contains(c, dockId)));



    // other helpers



    private bool IsFreeName(string name, DockModel? self)
    {
        if (name == "")
        {
            AppLog.WriteError("dock needs a name");
            return false;
        }

        if (FindDock(name) is { } other && other != self)
        {
            AppLog.WriteError($"dock '{name}' already exists");
            return false;
        }

        return true;
    }



    private void OnDockChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        foreach (var dock in _docks)
        {
            dock.Changed -= OnDockChanged;
            dock.Dispose();
        }
    }
}
