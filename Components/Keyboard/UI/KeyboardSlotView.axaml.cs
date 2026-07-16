using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Components.Core.Dragging;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardSlotView : UserControl
{
    public KeyboardSlotView()
    {
        InitializeComponent();
    }



    private void OnKeyDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e);



    private void OnKeyDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e);



    private void OnKeyDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e);



    private void OnKeyHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);



    private void OnKeyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: DisplayKey key } border) return;

        var props = e.GetCurrentPoint(border).Properties;

        if (DataContext is not KeyboardSlotViewModel vm) return;

        if (props.IsRightButtonPressed)
        {
            vm.OnKeyRightClicked(key);
            e.Handled = true;
            return;
        }

        if (props.IsLeftButtonPressed)
            vm.OnKeyLeftClicked(key);
    }
}