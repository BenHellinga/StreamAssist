using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using StreamAssist.Components.OutputConsole.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Chatbot.UI;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.UI;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Dashboard.Background;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.Dashboard.UI;
using StreamAssist.Components.Theme.Background;
using StreamAssist.Components.Theme.Model;
using StreamAssist.Components.Theme.UI;

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
    private DataModel _dataModel;
    private DashboardComponentModel _dashboardComponentModel;
    private ThemeComponentModel _themeComponentModel;

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
    private DataStore _dataStore;
    private DashboardSaveManager _dashboardSaveManager;
    private TimerManager _timerManager;
    private AppStartedManager _appStartedManager;
    private ThemeManager _themeManager;

    // main view models
    private PanelViewModel _outputConsolePanelViewModel;
    private PanelViewModel _bottomPanelViewModel;
    private PanelViewModel _sidePanelViewModel;

    // tabs
    private ObservableCollection<ViewModelTabItem> _tabs;
    private Dictionary<string, string> _tabFiles; // tab → the file it shows, the tab is replaced by a notice while it can't be read
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
        // config files report problems to the console (before anything loads)

        JsonFileStore.ReportError = AppLog.WriteError;
        JsonFileStore.ReportWarning = AppLog.WriteWarning;
        AppPaths.EnsureAssetFolders(); // Assets/Sounds, Images, Videos, for the user's files

        // the theme next, so the window is drawn with it from the start

        var freshTheme = !JsonFileStore.Exists(ThemeManager.FileName);
        _themeComponentModel = ThemeManager.Load();
        if (freshTheme) _themeComponentModel.Configs.TryCreate(ConfigSet<ThemeConfigModel>.DefaultName);
        _themeManager = new(_themeComponentModel); // reads the shipped defaults, applies the user's changes, saves new ones

        // data (tables and variables) before the other models, so blocks loading below can already look up variables

        _dataModel = new();
        _dataStore = new(_dataModel, System.IO.Path.Combine(JsonFileStore.RootDir, "data.db")); // loads it, then saves every change
        GlobalContext.DataModel = _dataModel;
        TriggerContext.VariableText = name =>
            _dataModel.VariableKind(name) != null && _dataModel.TryGetVariable(name, out _, out var value) ? ValueTypes.ToText(value) : null;

        // models (a tab whose file doesn't exist yet, the first time, starts with a config named Default so it can be used
        // right away; deleting every config later leaves it with none, and that's what's saved)

        var freshKeyboard = !JsonFileStore.Exists("keyboard");
        var freshChatbot = !JsonFileStore.Exists(ChatbotSaveManager.FileName);
        var freshDashboard = !JsonFileStore.Exists(DashboardSaveManager.FileName);

        _windowLayoutModel = WindowLayoutSaveManager.Load();
        _keyboardComponentModel = JsonFileStore.Load<KeyboardComponentModel>("keyboard");
        _chatbotComponentModel = ChatbotSaveManager.Load();
        _audioComponentModel = AudioSaveManager.Load();
        _obsComponentModel = ObsSaveManager.Load();
        _spotifyComponentModel = SpotifySaveManager.Load();
        _dashboardComponentModel = DashboardSaveManager.Load();

        if (freshKeyboard) _keyboardComponentModel.ListModel.Configs.TryCreate(ConfigSet<KeyboardConfigModel>.DefaultName);
        if (freshChatbot)
        {
            _chatbotComponentModel.ChatConfigs.TryCreate(ConfigSet<ChatConfigModel>.DefaultName);
            _chatbotComponentModel.EventsConfigs.TryCreate(ConfigSet<EventsConfigModel>.DefaultName);
        }
        if (freshDashboard) _dashboardComponentModel.Configs.TryCreate(ConfigSet<DashboardConfigModel>.DefaultName);

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

        _dashboardSaveManager = new(_dashboardComponentModel);
        _timerManager = new(_dashboardComponentModel);

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
        GlobalContext.DashboardComponentModel = _dashboardComponentModel;

        // the App Started event, once the startup connection attempts are over (set up last, it uses the managers)

        _appStartedManager = new(_chatbotComponentModel, () =>
            _chatbotManager.StreamerConnection == ConnectionStatus.Connecting || _chatbotManager.BotConnection == ConnectionStatus.Connecting
            || _voicemeeterManager.Status == ConnectionStatus.Connecting || _obsManager.Status == ConnectionStatus.Connecting
            || _spotifyManager.Status == ConnectionStatus.Connecting);

        // view models

        _outputConsolePanelViewModel = new(_windowLayoutModel, WindowLayoutModel.OutputConsolePanel);
        _outputConsolePanelViewModel.TransferContent(new OutputConsoleViewModel());
        _sidePanelViewModel = new(_windowLayoutModel, WindowLayoutModel.SidePanel);
        _bottomPanelViewModel = new(_windowLayoutModel, WindowLayoutModel.BottomPanel);

        // tab
        _tabFiles = new()
        {
            ["Dashboard"] = DashboardSaveManager.FileName,
            ["Keyboard"] = "keyboard",
            ["Chat"] = "chatbot",
            ["Events"] = "chatbot",
            ["Connections"] = CredentialStore.FileName,
        };
        _tabs = [
            new("Dashboard", () => Guarded("Dashboard", () => DashboardComponentViewModel.ActivateTab(_dashboardComponentModel, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel))),
            new("Keyboard", () => Guarded("Keyboard", () => KeyboardComponentViewModel.ActivateTab(_keyboardComponentModel, _keyboardComponentManager, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel))),
            new("Chat", () => Guarded("Chat", () => ChatbotComponentViewModel.ActivateChatTab(_chatbotComponentModel, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel))),
            new("Events", () => Guarded("Events", () => ChatbotComponentViewModel.ActivateEventsTab(_chatbotComponentModel, _windowLayoutModel, _sidePanelViewModel, _bottomPanelViewModel))),
            new("Data", () => DataComponentViewModel.ActivateTab(_dataModel, _sidePanelViewModel, _bottomPanelViewModel)),
            new("Connections", () => Guarded("Connections", () => ConnectionsComponentViewModel.ActivateTab(_chatbotComponentModel, _chatbotManager, _audioComponentModel, _voicemeeterManager, _obsComponentModel, _obsManager, _spotifyComponentModel, _spotifyManager, _sidePanelViewModel, _bottomPanelViewModel))),
            new("Themes", () => ThemeComponentViewModel.ActivateTab(_themeComponentModel, _sidePanelViewModel, _bottomPanelViewModel)),
        ];

        // reopen the last selected tab (set through the field so the content is only created once)
        _selectedTab = _tabs.FirstOrDefault(t => t.Label == _windowLayoutModel.SelectedTab) ?? _tabs[0];
        _activeContent = _selectedTab.CreateTabViewModel();
        _selectedTab.IsSelected = true;

        _windowLayoutModel.SelectedTabChanged += OnLayoutSelectedTabChanged;
        JsonFileStore.BrokenChanged += OnFileBrokenChanged;
    }



    // tab callbacks



    // a tab button was clicked (or a drag hovered over it)
    [RelayCommand] private void OnTabPressed(ViewModelTabItem tab) => SelectedTab = tab;



    partial void OnSelectedTabChanged(ViewModelTabItem value)
    {
        foreach (var tab in _tabs) tab.IsSelected = tab == value;

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



    // a file became unreadable or was backed up: the open tab showing it switches between the notice and its content
    private void OnFileBrokenChanged(string file) => Dispatcher.UIThread.Post(() =>
    {
        if (!_tabFiles.TryGetValue(SelectedTab.Label, out var shown) || shown != file) return;

        var oldContent = ActiveContent;
        ActiveContent = SelectedTab.CreateTabViewModel();
        oldContent.Dispose();
    });



    // helpers



    // the tab's content, or the broken file notice while its file can't be read
    private ViewModelBase Guarded(string tab, System.Func<ViewModelBase> create) =>
        _tabFiles.TryGetValue(tab, out var file) && JsonFileStore.IsBroken(file)
            ? BrokenFileViewModel.ActivateTab(file, _sidePanelViewModel, _bottomPanelViewModel)
            : create();



    // disposal



    public override void Dispose()
    {
        _windowLayoutModel.SelectedTabChanged -= OnLayoutSelectedTabChanged;
        JsonFileStore.BrokenChanged -= OnFileBrokenChanged;

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

        _dataStore.Dispose();

        _timerManager.Dispose();
        _appStartedManager.Dispose();
        _dashboardSaveManager.Dispose();
        _dashboardComponentModel.Dispose();

        _chatCommandManager.Dispose();
        _chatEventManager.Dispose();
        _chatbotManager.Dispose();
        _chatbotSaveManager.Dispose();
        _keyboardComponentManager.Dispose();
        _windowLayoutSaveManager.Dispose();
        _themeManager.Dispose();
    }
}