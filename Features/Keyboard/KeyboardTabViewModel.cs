using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardTabViewModel : ViewModelBase
{
    public KeyboardListViewModel KeyboardList { get; }
    public ActionListViewModel ActionList { get; } = new();
    public ActionPanelViewModel ActionPanel { get; } = new();

    [ObservableProperty] private GridLength _actionListWidth = new(300);
    [ObservableProperty] private GridLength _actionPanelHeight = new(160);



    // constructor



    public KeyboardTabViewModel(InputManager inputManager, KeyboardListData settings)
    {
        KeyboardList = new KeyboardListViewModel(inputManager, settings, ActionPanel);
    }
}