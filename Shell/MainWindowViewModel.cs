using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard.UI;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.OutputConsole.UI;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.Connections.UI;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Chatbot.UI;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;

namespace StreamAssist.Shell;



public partial class MainWindowViewModel : ViewModelBase
{
    // models
    private WindowLayoutModel _windowLayoutModel;
    private KeyboardComponentModel _keyboardComponentModel;
    private ChatbotComponentModel _chatbotComponentModel;
    private AudioComponentModel _audioComponentModel;
    private ObsComponentModel _obsComponentModel;
    private SpotifyComponentModel _spotifyComponentModel;

    // managers
    private WindowLayoutSaveManager _windowLayoutSaveManager;
    private KeyboardComponentManager _keyboardComponentManager;
    private ChatbotSaveManager _chatbotSaveManager;
    private ChatbotManager _chatbotManager;
    private ChatCommandManager _chatCommandManager;
    private ChatEventManager _chatEventManager;
    private AudioSaveManager _audioSaveManager;

    private VoicemeeterManager _voicemeeterManager; // this should go in an AudioComponentManager
    private ObsSaveManager _obsSaveManager;
    private ObsManager _obsManager;
    private SpotifySaveManager _spotifySaveManager;
    private SpotifyManager _spotifyManager;

    // main view models
    private PanelViewModel _outputConsolePanelViewModel;
    private PanelViewModel _bottomPanelViewModel;
    private PanelViewModel _sidePanelViewModel;

    // tabs
    private ObservableCollection<ViewModelTabItem> _tabs;
    [ObservableProperty] private ViewModelTabItem _selectedTab;
    [ObservableProperty] private ViewModelBase _activeContent;

    // public getter properties
    public WindowLayoutModel WindowLayout => _windowLayoutModel;
    public KeyboardComponentManager KeyboardComponentManager => _keyboardComponentManager;

    public PanelViewModel OutputConsolePanel => _outputConsolePanelViewModel;
    public PanelViewModel SidePanel => _sidePanelViewModel;
    public PanelViewModel BottomPanel => _bottomPanelViewModel;
    public ObservableCollection<ViewModelTabItem> Tabs => _tabs;



    // constructor



    public MainWindowViewModel()
    {
        // models

        _windowLayoutModel = WindowLayoutSaveManager.Load();
        _keyboardComponentModel = JsonFileStore.Load<KeyboardComponentModel>("keyboard");
        _chatbotComponentModel = ChatbotSaveManager.Load();
        _audioComponentModel = AudioSaveManager.Load();
        _obsComponentModel = ObsSaveManager.Load();
        _spotifyComponentModel = SpotifySaveManager.Load();

        // managers

        _windowLayoutSaveManager = new(_windowLayoutModel);
        _keyboardComponentManager = new(_keyboardComponentModel);
        _chatbotSaveManager = new(_chatbotComponentModel);
        _chatbotManager = new(_chatbotComponentModel);
        _chatCommandManager = new(_chatbotComponentModel, _chatbotManager);
        _chatEventManager = new(_chatbotComponentModel, _chatbotManager);

        _audioSaveManager = new(_audioComponentModel);
        _voicemeeterManager = new(_audioComponentModel); // connects if connect on startup is ticked

        _obsSaveManager = new(_obsComponentModel);
        _obsManager = new(_obsComponentModel); // connects if connect on startup is ticked

        _spotifySaveManager = new(_spotifyComponentModel);
        _spotifyManager = new(_spotifyComponentModel); // connects if connect on startup is ticked

        // global context

        GlobalContext.KeyboardComponentModel = _keyboardComponentModel;
        GlobalContext.KeyboardInputManager = _keyboardComponentManager.GetInputManager();
        GlobalContext.VoicemeeterManager = _voicemeeterManager;
        GlobalContext.AudioComponentModel = _audioComponentModel;
        GlobalContext.WindowLayoutModel = _windowLayoutModel;
        GlobalContext.ChatbotComponentModel = _chatbotComponentModel;
        GlobalContext.ChatbotManager = _chatbotManager;
        GlobalContext.ObsComponentModel = _obsComponentModel;
        GlobalContext.ObsManager = _obsManager;
        GlobalContext.SpotifyComponentModel = _spotifyComponentModel;
        GlobalContext.SpotifyManager = _spotifyManager;

        // view models

        _outputConsolePanelViewModel = new(_windowLayoutModel, WindowLayoutModel.OutputConsolePanel);
        _outputConsolePanelViewModel.TransferContent(new OutputConsoleViewModel());
        _sidePanelViewModel = new(_windowLayoutModel, WindowLayoutModel.SidePanel);
        _bottomPanelViewModel = new(_windowLayoutModel, WindowLayoutModel.BottomPanel);

        // tab
        _tabs = [
            new("Keyboard", () => KeyboardComponentViewModel.ActivateTab(_keyboardComponentModel, _keyboardComponentManager, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel)),
            new("Chatbot", () => ChatbotComponentViewModel.ActivateChatTab(_chatbotComponentModel, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel)),
            new("Events", () => ChatbotComponentViewModel.ActivateEventsTab(_chatbotComponentModel, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel)),
            new("Connections", () => ConnectionsComponentViewModel.ActivateTab(_chatbotComponentModel, _chatbotManager, _audioComponentModel, _voicemeeterManager, _obsComponentModel, _obsManager, _spotifyComponentModel, _spotifyManager, _sidePanelViewModel, _bottomPanelViewModel)),
        ];

        // reopen the last selected tab (set through the field so the content is only created once)
        _selectedTab = _tabs.FirstOrDefault(t => t.Label == _windowLayoutModel.SelectedTab) ?? _tabs[0];
        _activeContent = _selectedTab.CreateTabViewModel();

        _windowLayoutModel.SelectedTabChanged += OnLayoutSelectedTabChanged;
    }



    // tab callbacks



    partial void OnSelectedTabChanged(ViewModelTabItem value)
    {
        ViewModelBase oldContent = ActiveContent;
        ActiveContent = value.CreateTabViewModel();
        oldContent?.Dispose();

        _windowLayoutModel.SetSelectedTab(value.Label);
    }



    // something else (e.g. an action) changed the selected tab
    private void OnLayoutSelectedTabChanged(string? label)
    {
        var tab = _tabs.FirstOrDefault(t => t.Label == label);
        if (tab != null && tab != SelectedTab) SelectedTab = tab;
    }



    // disposal



    public override void Dispose()
    {
        _windowLayoutModel.SelectedTabChanged -= OnLayoutSelectedTabChanged;

        ActiveContent.Dispose();

        _bottomPanelViewModel.Dispose();
        _sidePanelViewModel.Dispose();
        _outputConsolePanelViewModel.Dispose();

        _voicemeeterManager.Dispose();
        _audioSaveManager.Dispose();

        _obsManager.Dispose();
        _obsSaveManager.Dispose();

        _spotifyManager.Dispose();
        _spotifySaveManager.Dispose();

        _chatCommandManager.Dispose();
        _chatEventManager.Dispose();
        _chatbotManager.Dispose();
        _chatbotSaveManager.Dispose();
        _keyboardComponentManager.Dispose();
        _windowLayoutSaveManager.Dispose();
    }
}