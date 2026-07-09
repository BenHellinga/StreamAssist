using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;
using StreamAssist.Components.StreamAction;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardTabViewModel : ViewModelBase
{
    public KeyboardListViewModel KeyboardList { get; }
    public ActionListViewModel ActionList { get; } = new();
    public ActionPanelViewModel ActionPanel { get; } = new();

    [ObservableProperty] private GridLength _actionListWidth = new(300);
    [ObservableProperty] private GridLength _actionPanelHeight = new(160);



    // constructor



    public KeyboardTabViewModel(KeyboardInputManager inputManager, KeyboardListModel settings)
    {
        KeyboardList = new KeyboardListViewModel(inputManager, settings, ActionPanel);
    }
}