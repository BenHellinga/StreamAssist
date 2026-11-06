using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.UI;



public partial class InputListView : ViewBase
{
    public InputListView()
    {
        InitializeComponent();
    }



    // the line above each box, and the drop zone at the end: the list (this view's DataContext) takes the drop



    private void OnZoneDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnZoneDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnZoneDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);
}
