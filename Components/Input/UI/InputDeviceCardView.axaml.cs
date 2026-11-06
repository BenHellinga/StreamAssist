using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Input.UI;



public partial class InputDeviceCardView : UserControl
{
    public InputDeviceCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}
