using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



public partial class MatchTextRowListView : ViewBase
{
    public MatchTextRowListView()
    {
        InitializeComponent();
    }



    // the line above each row: this view's DataContext reorders them, the border is the one to light up



    private void OnRowDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver(DataContext, e, sender as Border);



    private void OnRowDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave(DataContext, e, sender as Border);



    private void OnRowDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop(DataContext, e, sender as Border);
}
