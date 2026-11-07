using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// one config of the Widgets tab: its widgets, top to bottom. Names are unique (case ignored): blocks and the widgets'
// addresses find them by name
public sealed class WidgetsConfigModel : INamedConfig, IJsonOnDeserialized
{
    // private



    private string _name;
    private readonly List<WidgetModel> _widgets;



    // public



    [JsonIgnore] public string Name => _name;
    [JsonIgnore] public IReadOnlyList<WidgetModel> Widgets => _widgets;



    // json



    [JsonInclude] [JsonPropertyName("name")]
    private string JsonName
    {
        get => _name;
        set => _name = value;
    }

    [JsonInclude] [JsonPropertyName("widgets")]
    private List<WidgetModel> JsonWidgets
    {
        get => _widgets;
        set
        {
            _widgets.Clear();
            _widgets.AddRange(value);
        }
    }



    // events



    public event Action? Changed; // anything saved changed
    public event Action<WidgetModel, int>? WidgetAdded;
    public event Action<WidgetModel>? WidgetRemoved; // after it's taken out, before it's disposed
    public event Action<WidgetModel, int, int>? WidgetMoved; // where it was, where it is now



    // constructor



    public WidgetsConfigModel()
    {
        _name = "";
        _widgets = [];
    }



    // loading: names made unique, then the widgets are listened to
    public void OnDeserialized()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var widget in _widgets)
        {
            var name = widget.Name.Trim() == "" ? WidgetModel.KindTitle(widget.Kind) : widget.Name.Trim();
            var unique = name;
            for (var n = 2; !seen.Add(unique); n++) unique = $"{name} {n}";
            widget.SetName(unique);
            widget.Changed += OnWidgetChanged;
        }
    }



    // public api



    public void SetName(string name) => _name = name;



    public WidgetModel? FindWidget(string name) =>
        _widgets.FirstOrDefault(w => string.Equals(w.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));



    // a new widget of that kind at index (the end if it's past it), named after its kind ("Wheel", then "Wheel 2", ...)
    public bool TryAddWidget(WidgetKind kind, int index, out WidgetModel widget)
    {
        widget = WidgetModel.Create(kind, NewName(WidgetModel.KindTitle(kind)));
        Insert(widget, index);
        return true;
    }



    // a copied widget pasted at index: its own name if that's free, otherwise a numbered one ("Wheel 2")
    public bool TryPasteWidget(WidgetModel copied, int index)
    {
        var name = FindWidget(copied.Name) == null ? copied.Name : NewName(copied.Name);
        if (WidgetModel.CopyOf(copied, name) is not { } copy)
        {
            AppLog.WriteError($"can't paste widget '{copied.Name}'");
            return false;
        }

        Insert(copy, index);
        return true;
    }



    public bool TryRenameWidget(WidgetModel widget, string name)
    {
        var trimmed = name.Trim();
        if (!_widgets.Contains(widget))
        {
            AppLog.WriteError($"can't rename widget '{widget.Name}', not found");
            return false;
        }

        if (trimmed == widget.Name) return true;

        if (trimmed == "")
        {
            AppLog.WriteError("widget needs a name");
            return false;
        }

        if (FindWidget(trimmed) is { } other && other != widget)
        {
            AppLog.WriteError($"widget '{trimmed}' already exists");
            return false;
        }

        widget.SetName(trimmed);
        return true;
    }



    // to just before the widget now at index (the end if it's past it)
    public bool TryMoveWidget(WidgetModel widget, int index)
    {
        var from = _widgets.IndexOf(widget);
        if (from < 0)
        {
            AppLog.WriteError($"can't move widget '{widget.Name}', not found");
            return false;
        }

        var to = Math.Clamp(index > from ? index - 1 : index, 0, _widgets.Count - 1);
        if (to == from) return true;

        _widgets.RemoveAt(from);
        _widgets.Insert(to, widget);
        WidgetMoved?.Invoke(widget, from, to);
        Changed?.Invoke();
        return true;
    }



    public bool TryRemoveWidget(WidgetModel widget)
    {
        if (!_widgets.Remove(widget))
        {
            AppLog.WriteError($"can't remove widget '{widget.Name}', not found");
            return false;
        }

        widget.Changed -= OnWidgetChanged;
        WidgetRemoved?.Invoke(widget);
        Changed?.Invoke();
        widget.Dispose(); // after the views let go of it
        return true;
    }



    public string NewName(string title)
    {
        var name = title;
        for (var n = 2; FindWidget(name) != null; n++) name = $"{title} {n}";
        return name;
    }



    // helpers



    private void Insert(WidgetModel widget, int index)
    {
        var at = Math.Clamp(index, 0, _widgets.Count);
        _widgets.Insert(at, widget);
        widget.Changed += OnWidgetChanged;
        WidgetAdded?.Invoke(widget, at);
        Changed?.Invoke();
    }



    private void OnWidgetChanged() => Changed?.Invoke();



    // disposal



    public void Dispose()
    {
        foreach (var widget in _widgets)
        {
            widget.Changed -= OnWidgetChanged;
            widget.Dispose();
        }
    }
}
