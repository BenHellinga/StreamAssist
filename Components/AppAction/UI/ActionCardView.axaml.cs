using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Components.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionCardView : UserControl
{
    public ActionCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}