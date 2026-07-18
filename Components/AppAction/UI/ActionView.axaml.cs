using Avalonia.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionView : ViewBase
{
    public ActionView()
    {
        InitializeComponent();
    }



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}