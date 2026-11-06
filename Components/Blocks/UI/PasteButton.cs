using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



// a small paste button for a drop target (an empty slot, a value box, an unbound key or bind box): it shows while the
// pointer is on the target's Droppable border and what's on the clipboard would be taken there (BlockClipboard), and
// pressing it drops a fresh copy there. Its target is its DataContext (an IDroppable), as for a drop on that border
public class PasteButton : Button
{
    // private



    private Border? _host; // the Droppable border it sits in, whose hover shows it
    private bool _hovered;



    // looks like any other button (Square Small)
    protected override Type StyleKeyOverride => typeof(Button);



    // constructor



    public PasteButton()
    {
        Classes.Add("Square");
        Classes.Add("Small");
        IsVisible = false;

        var icon = new Path { Classes = { "Icon", "Line" } };
        icon.Bind(Path.DataProperty, icon.GetResourceObservable("PasteIcon"));
        Content = icon;
    }



    // visual tree



    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _host = this.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("Droppable"));
        if (_host != null)
        {
            _host.PointerEntered += OnHostPointerEntered;
            _host.PointerExited += OnHostPointerExited;
        }

        BlockClipboard.Changed += Refresh;
        Refresh();
    }



    // another target (a reused view)
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Refresh();
    }



    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_host != null)
        {
            _host.PointerEntered -= OnHostPointerEntered;
            _host.PointerExited -= OnHostPointerExited;
            _host = null;
        }

        BlockClipboard.Changed -= Refresh;
    }



    // ui events



    private void OnHostPointerEntered(object? sender, PointerEventArgs e)
    {
        _hovered = true;
        Refresh();
    }



    private void OnHostPointerExited(object? sender, PointerEventArgs e)
    {
        _hovered = false;
        Refresh();
    }



    protected override void OnClick()
    {
        base.OnClick();

        if (DataContext is IDroppable target) BlockClipboard.TryPasteOn(target, target);
        Refresh(); // the target is usually full now
    }



    // helpers



    // checked each time the pointer comes on (the target may have filled up since)
    private void Refresh() => IsVisible = _hovered && DataContext is IDroppable target && BlockClipboard.CanPasteOn(target);
}
