using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard.UI;
using StreamAssist.Components.Core;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.Audio.Background;

namespace StreamAssist.Components.Main.UI;



public partial class MainWindowViewModel : ViewModelBase
{
    // models
    private KeyboardComponentModel _keyboardComponentModel;

    // managers
    private KeyboardComponentManager _keyboardComponentManager;

    private VoicemeeterManager _voicemeeterManager; // this should go in an AudioComponentManager

    // main view models
    private PanelViewModel _outputConsolePanelViewModel;
    private PanelViewModel _bottomPanelViewModel;
    private PanelViewModel _sidePanelViewModel;

    // tabs
    private ObservableCollection<ViewModelTabItem> _tabs;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private ViewModelBase _activeContent;

    // public getter properties
    public KeyboardComponentManager KeyboardComponentManager => _keyboardComponentManager;

    public PanelViewModel OutputConsolePanel => _outputConsolePanelViewModel;
    public PanelViewModel SidePanel => _sidePanelViewModel;
    public PanelViewModel BottomPanel => _bottomPanelViewModel;
    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // constructor



    public MainWindowViewModel()
    {
        // models

        _keyboardComponentModel = JsonFileStore.Load<KeyboardComponentModel>("keyboard");

        // managers

        _keyboardComponentManager = new(_keyboardComponentModel);

        _voicemeeterManager = new();
        _voicemeeterManager.TryConnect();

        // global context

        GlobalContext.KeyboardComponentModel = _keyboardComponentModel;
        GlobalContext.VoicemeeterManager = _voicemeeterManager;

        // view models

        _outputConsolePanelViewModel = new();
        _outputConsolePanelViewModel.SetContent(new OutputConsoleViewModel());
        _sidePanelViewModel = new();
        _bottomPanelViewModel = new();

        // tab
        _tabs = [
            new("Keyboard", () => KeyboardComponentViewModel.ActivateTab(_keyboardComponentModel, _keyboardComponentManager, _sidePanelViewModel, _bottomPanelViewModel)),
            new("Test", () => { _bottomPanelViewModel.SetContent(null); return new OutputConsoleViewModel(); })
        ];

        SelectedTab = _tabs[0];
        ActiveContent = SelectedTab.CreateTabViewModel();

        AppLog.WriteLine("StreamAssist started");
    }



    // tab callbacks



    partial void OnSelectedTabChanged(ViewModelTabItem value)
    {
        ViewModelBase oldContent = ActiveContent;
        ActiveContent = value.CreateTabViewModel();
        oldContent?.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        ActiveContent.Dispose();

        _bottomPanelViewModel.Dispose();
        _sidePanelViewModel.Dispose();
        _outputConsolePanelViewModel.Dispose();

        _voicemeeterManager.Dispose();

        _keyboardComponentManager.Dispose();
    }
}