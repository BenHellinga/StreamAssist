using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Dashboard.UI;



public partial class ButtonDockView : UserControl
{
    public ButtonDockView()
    {
        InitializeComponent();
    }



    // each bind box's DataContext is its row



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver((sender as Control)?.DataContext, e, sender as Border);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave((sender as Control)?.DataContext, e, sender as Border);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop((sender as Control)?.DataContext, e, sender as Border);



    // the line above each row: the rows reorder (this view's DataContext), the border is the one to light up



    private void OnRowDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnRowDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnRowDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);



    private void OnBindBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not DockButtonRowViewModel row) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        row.OnBindBoxClicked();
        e.Handled = true;
    }
}
