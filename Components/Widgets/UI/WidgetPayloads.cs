using System;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.UI;



// a card from the widget list being dragged into the widgets (adds one of that kind where it's dropped)
public sealed class NewWidgetPayload(WidgetKind kind)
{
    public WidgetKind Kind => kind;
}



// a widget box being dragged by its grip to another place in the list (moves it)
public sealed class WidgetDragPayload(WidgetModel widget)
{
    public WidgetModel Widget => widget;
}



// a copied widget (BlockClipboard's "widget" kind), pasted onto the list's drop zone: a copy of it goes there
public sealed class WidgetCopy(WidgetModel widget) : IDisposable
{
    public const string Kind = "widget";

    public WidgetModel Widget => widget;

    public void Dispose() => widget.Dispose();
}
