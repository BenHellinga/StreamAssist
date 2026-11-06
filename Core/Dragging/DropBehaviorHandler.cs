using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace StreamAssist.Core.Dragging;



// the drop target nearest the pointer handles a drag (marked handled), so a target inside another (a block in a list,
// a slot inside that block) never also drops into the outer one, unless it passes the payload on (IDroppable.PassesOn):
// then the event goes on up, and the outer target's handler names its own border (the one it lights up), since the
// nearest Droppable border to the pointer is still the inner one
public static class DropBehaviorHandler
{
    public static void OnDragOver(object? target, DragEventArgs e, Border? ownBorder = null)
    {
        e.DragEffects = DragDropEffects.None;

        if ((ownBorder ?? FindDroppableBorder(e.Source)) is not { } border) return;

        var payload = e.DataTransfer.TryGetValue(DragDropPayload.Format);
        if (PassesOn(target, payload)) return;
        e.Handled = true;

        if (payload is null) return;

        if (target is not IDroppable droppable) return;
        if (!droppable.CanAcceptDropPayload(payload)) return;

        e.DragEffects = DragDropEffects.Move;
        border.Classes.Set("Dragover", true);
    }



    public static void OnDragLeave(object? target, DragEventArgs e, Border? ownBorder = null)
    {
        if ((ownBorder ?? FindDroppableBorder(e.Source)) is not { } border) return;
        if (PassesOn(target, e.DataTransfer.TryGetValue(DragDropPayload.Format))) return;
        e.Handled = true;

        border.Classes.Set("Dragover", false);
    }



    public static void OnDrop(object? target, DragEventArgs e, Border? ownBorder = null)
    {
        if ((ownBorder ?? FindDroppableBorder(e.Source)) is not { } border) return;

        var payload = e.DataTransfer.TryGetValue(DragDropPayload.Format);
        if (PassesOn(target, payload)) return;
        e.Handled = true;

        border.Classes.Set("Dragover", false);
        if (payload is null) return;

        if (target is not IDroppable droppable) return;
        if (!droppable.CanAcceptDropPayload(payload)) return;

        droppable.OnDroppedOn(payload, border.DataContext);
    }



    // private



    private static bool PassesOn(object? target, object? payload) =>
        payload is not null && target is IDroppable droppable && droppable.PassesOn(payload);



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