using Avalonia.Controls;
using Avalonia.Input;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



public partial class MicBounceBodyView : ViewBase
{
    public MicBounceBodyView()
    {
        InitializeComponent();
    }
}



public partial class WheelBodyView : ViewBase
{
    public WheelBodyView()
    {
        InitializeComponent();
    }
}



public partial class PlinkoBodyView : ViewBase
{
    public PlinkoBodyView()
    {
        InitializeComponent();
    }
}



public partial class ClipBodyView : ViewBase
{
    public ClipBodyView()
    {
        InitializeComponent();
    }
}



public partial class ImagePartyBodyView : ViewBase
{
    public ImagePartyBodyView()
    {
        InitializeComponent();
    }



    // the bind box (its DataContext is the bind box's view model): a binding dropped on it, clicked to edit below



    private void OnDragOver(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragOver((sender as Control)?.DataContext, e, sender as Border);



    private void OnDragLeave(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDragLeave((sender as Control)?.DataContext, e, sender as Border);



    private void OnDrop(object? sender, DragEventArgs e) => DropBehaviorHandler.OnDrop((sender as Control)?.DataContext, e, sender as Border);



    private void OnBindBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not CornerBindBoxViewModel box) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        box.OnBindBoxClicked();
        e.Handled = true;
    }
}
