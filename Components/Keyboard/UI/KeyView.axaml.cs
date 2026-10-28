using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyView : ViewBase
{
    public KeyView()
    {
        InitializeComponent();
    }



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e);



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not KeyViewModel vm) return;
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
            vm.OnPointerLeftClicked();
    }
}
