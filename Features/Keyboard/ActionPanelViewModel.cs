using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



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