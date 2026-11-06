using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Blocks.UI;



// view models shown with EditButtons (copy if CanCopy, collapse if CanCollapse, enable/disable if CanToggle, drag
// handle, remove)
public interface IEditButtons : IDraggable
{
    IRelayCommand ToggleButtonPressedCommand { get; }
    IRelayCommand RemoveButtonPressedCommand { get; }
    IRelayCommand CollapseButtonPressedCommand { get; }
    IRelayCommand CopyButtonPressedCommand { get; } // puts it on the clipboard (BlockClipboard)
    bool CanCollapse { get; }
    bool CanCopy { get; }
    bool CanToggle { get; }
    bool IsDisabled { get; } // the toggle shows － while it's on, ＋ while it's off
}
