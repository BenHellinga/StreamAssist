using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Main;



public partial class MainWindowViewModel : ViewModelBase
{
    // models



    public KeyboardListModel KeyboardListModel { get; }



    // background



    // keyboard
    public KeyboardInputManager KeyboardInputManager { get; }
    public KeyboardListManager KeyboardListManager { get; }



    // view models



    public KeyboardTabViewModel KeyboardTabViewModel { get; }
    public OutputConsoleViewModel OutputConsoleViewModel { get; } = new();




    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;




    public MainWindowViewModel()
    {
        // load models
        KeyboardListModel = JsonFileStore.Load<KeyboardListModel>("keyboard");



        // load managers
        KeyboardInputManager = new();
        KeyboardListModel.SetDevices(KeyboardInputManager.Devices);

        KeyboardListManager = new(KeyboardInputManager, KeyboardListModel);



        // load viewmodels
        KeyboardTabViewModel = new KeyboardTabViewModel(KeyboardInputManager, KeyboardListModel);

        Tabs.Add(new TabItemViewModel("Keyboard", KeyboardTabViewModel));
        Tabs.Add(new TabItemViewModel("test", OutputConsoleViewModel));

        SelectedTab = Tabs[0];

        AppLog.WriteLine("StreamAssist started");
    }

    public void Dispose()
    {
        KeyboardInputManager.Dispose();
        // TODO add more disposes
    }
}




public class TabItemViewModel(string title, object content)
{
    public string Title { get; } = title;
    public object Content { get; } = content;
}