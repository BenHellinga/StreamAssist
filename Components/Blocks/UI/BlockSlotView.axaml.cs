using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



public partial class BlockSlotView : ViewBase
{
    public BlockSlotView()
    {
        InitializeComponent();
    }



    private void OnZoneDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e);



    private void OnZoneDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e);



    private void OnZoneDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e);
}
