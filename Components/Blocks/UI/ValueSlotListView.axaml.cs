using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class ValueSlotListView : ViewBase
{
    public ValueSlotListView()
    {
        InitializeComponent();
    }



    // the line above each item: the list reorders (this view's DataContext), the border is the one to light up



    private void OnZoneDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnZoneDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnZoneDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);
}
