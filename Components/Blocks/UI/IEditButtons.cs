using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



// view models shown with EditButtons (drag handle, enable/disable, remove)
public interface IEditButtons : IDraggable
{
    IRelayCommand ToggleButtonPressedCommand { get; }
    IRelayCommand RemoveButtonPressedCommand { get; }
}
