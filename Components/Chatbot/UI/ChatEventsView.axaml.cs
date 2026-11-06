using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



public partial class ChatEventsView : ViewBase
{
    public ChatEventsView()
    {
        InitializeComponent();
    }



    // the line above each redeem row: this view's DataContext reorders them, the border is the one to light up



    private void OnRedeemDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnRedeemDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnRedeemDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);
}
