using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



public partial class ChatTargetView : ViewBase
{
    public ChatTargetView()
    {
        InitializeComponent();
    }



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);



    private void OnBindBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ChatTargetViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        vm.OnBindBoxClicked();
        e.Handled = true;
    }
}
