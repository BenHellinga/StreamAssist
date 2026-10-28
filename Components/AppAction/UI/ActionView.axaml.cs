using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionView : ViewBase
{
    public ActionView()
    {
        InitializeComponent();
    }



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}