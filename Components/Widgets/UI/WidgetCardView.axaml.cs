using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Widgets.UI;



public partial class WidgetCardView : UserControl
{
    public WidgetCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}
