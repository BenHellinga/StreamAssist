using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardDisplay : UserControl
{
    public KeyboardDisplay()
    {
        InitializeComponent();

        AddHandler(DragDrop.DropEvent,      OnDrop);
        AddHandler(DragDrop.DragOverEvent,  OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);

        DragDrop.SetAllowDrop(this, true);
    }



    // drag-drop handlers



    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(ActionMenu.DragDataFormat)) { e.DragEffects = DragDropEffects.None; return; }

        e.DragEffects = DragDropEffects.Copy;

        var border = HitTestKeyBorder(e.GetPosition(this));
        if (border is not null)
            border.Classes.Add("dragover");
    }



    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        // Clear all dragover highlights when leaving
        ClearDragOverHighlights();
    }



    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDragOverHighlights();

        if (DataContext is not KeyboardDisplayViewModel vm) return;
        if (!e.Data.Contains(ActionMenu.DragDataFormat))   return;

        var action = e.Data.Get(ActionMenu.DragDataFormat) as ActionEntry;
        if (action is null) return;

        var border = HitTestKeyBorder(e.GetPosition(this));
        if (border?.DataContext is not KeyboardKeyInfo key) return;

        vm.BindAction(key, action);
    }



    // helpers



    private Border? HitTestKeyBorder(Avalonia.Point position)
    {
        // Walk the visual tree to find a key Border under the pointer.
        // The Viewbox scales coordinates, so we use HitTestCustom.
        var hit = this.InputHitTest(position);
        var element = hit as Avalonia.Visual;
        while (element is not null)
        {
            if (element is Border b && b.Classes.Contains("key"))
                return b;
            element = element.GetVisualParent();
        }
        return null;
    }



    private void ClearDragOverHighlights()
    {
        foreach (var border in this.GetVisualDescendants().OfType<Border>())
            if (border.Classes.Contains("key"))
                border.Classes.Remove("dragover");
    }
}