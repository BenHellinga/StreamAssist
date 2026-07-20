using Avalonia.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionUIButtons : ViewBase
{
    public ActionUIButtons()
    {
        InitializeComponent();
    }



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}