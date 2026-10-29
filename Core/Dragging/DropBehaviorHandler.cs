using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace StreamAssist.Core.Dragging;



// the drop target nearest the pointer handles a drag (marked handled), so a target inside another (a block in a list,
// a slot inside that block) never also drops into the outer one
public static class DropBehaviorHandler
{
    public static void OnDragOver(object? target, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        if (FindDroppableBorder(e.Source) is not { } border) return;
        e.Handled = true;

        var payload = e.DataTransfer.TryGetValue(DragDropPayload.Format);
        if (payload is null) return;

        if (target is not IDroppable droppable) return;
        if (!droppable.CanAcceptDropPayload(payload)) return;

        e.DragEffects = DragDropEffects.Move;
        border.Classes.Set("Dragover", true);
    }



    public static void OnDragLeave(object? target, DragEventArgs e)
    {
        if (FindDroppableBorder(e.Source) is not { } border) return;
        e.Handled = true;

        border.Classes.Set("Dragover", false);
    }



    public static void OnDrop(object? target, DragEventArgs e)
    {
        if (FindDroppableBorder(e.Source) is not { } border) return;
        e.Handled = true;

        border.Classes.Set("Dragover", false);
        var payload = e.DataTransfer.TryGetValue(DragDropPayload.Format);
        if (payload is null) return;

        if (target is not IDroppable droppable) return;
        if (!droppable.CanAcceptDropPayload(payload)) return;

        droppable.OnDroppedOn(payload, border.DataContext);
    }



    // private



    private static Border? FindDroppableBorder(object? source)
    {
        for (var current = source as Visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Border { Classes: var classes } border && classes.Contains("Droppable"))
                return border;
        }

        return null;
    }
}