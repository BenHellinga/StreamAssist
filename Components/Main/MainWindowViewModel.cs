using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Main;



public partial class MainWindowViewModel : ViewModelBase
{
    // models



    public KeyboardComponentModel KeyboardComponentModel { get; }



    // managers



    public KeyboardComponentManager KeyboardComponentManager { get; }



    // view models



    public KeyboardTabViewModel KeyboardTabViewModel { get; }
    public OutputConsoleViewModel OutputConsoleViewModel { get; } = new();




    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;




    public MainWindowViewModel()
    {
        // init models



        KeyboardComponentModel = JsonFileStore.Load<KeyboardComponentModel>("keyboard");



        // init managers



        KeyboardComponentManager = new(KeyboardComponentModel);



        ChangeConfigAction.Model = KeyboardComponentModel;


        // load viewmodels
        KeyboardTabViewModel = new KeyboardTabViewModel(KeyboardComponentManager.GetInputManager(), KeyboardComponentModel);

        Tabs.Add(new TabItemViewModel("Keyboard", KeyboardTabViewModel));
        Tabs.Add(new TabItemViewModel("test", OutputConsoleViewModel));

        SelectedTab = Tabs[0];

        AppLog.WriteLine("StreamAssist started");
    }

    public void Dispose()
    {
        KeyboardComponentManager.Dispose();
    }
}




public class TabItemViewModel(string title, object content)
{
    public string Title { get; } = title;
    public object Content { get; } = content;
}