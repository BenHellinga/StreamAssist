using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the ActionPanel view - the strip below the keyboard that shows
/// details for whichever bound key was last clicked. Empty until a
/// selection comes in via ShowKey, and clears itself if the clicked key
/// turns out to have no BoundAction.
/// </summary>
public partial class ActionPanelViewModel : ViewModelBase
{
    [ObservableProperty] private string _actionName = "";
    [ObservableProperty] private string _actionDescription = "";
    [ObservableProperty] private bool _hasSelection;



    public void ShowKey(KeyInfo? key)
    {
        if (key?.BoundAction is { } action)
        {
            ActionName = action.Name;
            ActionDescription = action.Description;
            HasSelection = true;
        }
        else
        {
            ActionName = "";
            ActionDescription = "";
            HasSelection = false;
        }
    }
}