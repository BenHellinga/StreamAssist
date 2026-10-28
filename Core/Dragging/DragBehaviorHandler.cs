using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace StreamAssist.Core.Dragging;



public static class DragBehaviorHandler
{
    public static async void OnDragStarted(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control source) return;

        var handle = source as Border ?? source.FindAncestorOfType<Border>();

        if (handle is null) return;
        if (!handle.Classes.Contains("Draggable")) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
        if (handle.DataContext is not IDraggable draggable) return;

        e.Handled = true;
        var payload = draggable.GetDragPayload();
        if (payload == null) return;

        var item = new DataTransferItem();
        item.Set(DragDropPayload.Format, payload);

        var data = new DataTransfer();
        data.Add(item);

        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }
}