using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace StreamAssist.Components.Core.Dragging;



public static class DropBehaviorHandler
{
    public static void OnDragOver(object? target, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;

        if (FindDroppableBorder(e.Source) is not { } border) return;

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

        border.Classes.Set("Dragover", false);
    }



    public static void OnDrop(object? target, DragEventArgs e)
    {
        if (FindDroppableBorder(e.Source) is not { } border) return;

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