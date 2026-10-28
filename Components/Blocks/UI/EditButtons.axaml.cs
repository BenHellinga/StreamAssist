using Avalonia.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



public partial class EditButtons : ViewBase
{
    public EditButtons()
    {
        InitializeComponent();
    }



    private void OnHandlePointerPressed(object? sender, PointerPressedEventArgs e) => DragBehaviorHandler.OnDragStarted(sender, e);
}
