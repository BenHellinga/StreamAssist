using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



public partial class BlockCardView : ViewBase
{
    public BlockCardView()
    {
        InitializeComponent();
    }



    private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}
