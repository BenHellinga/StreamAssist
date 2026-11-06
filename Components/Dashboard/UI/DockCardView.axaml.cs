using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Dashboard.UI;



public partial class DockCardView : UserControl
{
    public DockCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}
