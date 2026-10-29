using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class ValueSlotView : ViewBase
{
    public ValueSlotView()
    {
        InitializeComponent();
    }



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e);
}
