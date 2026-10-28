using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionUIButtons : ViewBase
{
    public ActionUIButtons()
    {
        InitializeComponent();
    }



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}