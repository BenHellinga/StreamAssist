using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public interface IActionUIButtons : IDraggable
{
    IRelayCommand ToggleButtonPressedCommand { get; }
    IRelayCommand RemoveButtonPressedCommand { get; }
}