using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionCardView : ViewBase
{
    public ActionCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}