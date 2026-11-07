using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Widgets.Background;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Connections.UI;



// the Connections tab: every outside program/service the app talks to, with its login details and status
// (no side or bottom panel). Connecting / disconnecting only goes through the models, the managers listen to them.
public partial class ConnectionsComponentViewModel : ViewModelBase
{
    // private



    private readonly ChatbotComponentModel _chatbotModel;
    private readonly ChatbotManager _chatbotManager;
    private readonly AudioComponentModel _audioModel;
    private readonly VoicemeeterManager _voicemeeterManager;
    private readonly ObsComponentModel _obsModel;
    private readonly ObsManager _obsManager;
    private readonly SpotifyComponentModel _spotifyModel;
    private readonly SpotifyManager _spotifyManager;
    private readonly WidgetsComponentModel _widgetsModel;
    private readonly WidgetManager _widgetManager;
    private readonly ConnectionRowViewModel _twitchStreamer;
    private readonly ConnectionRowViewModel _twitchBot;
    private readonly ConnectionRowViewModel _voicemeeter;
    private readonly ConnectionRowViewModel _obs;
    private readonly ConnectionRowViewModel _spotify;
    private readonly ConnectionRowViewModel _widgets;

    [ObservableProperty] private string _clientIdInput;
    [ObservableProperty] private string _clientSecretInput;
    [ObservableProperty] private string _obsAddressInput;
    [ObservableProperty] private string _obsPasswordInput;
    [ObservableProperty] private string _spotifyClientIdInput;
    [ObservableProperty] private string _widgetPortInput;



    // public



    public ConnectionRowViewModel TwitchStreamer => _twitchStreamer;
    public ConnectionRowViewModel TwitchBot => _twitchBot;
    public ConnectionRowViewModel Voicemeeter => _voicemeeter;
    public ConnectionRowViewModel Obs => _obs;
    public ConnectionRowViewModel Spotify => _spotify;
    public string SpotifyRedirectUri => SpotifyComponentModel.RedirectUri;
    public ConnectionRowViewModel Widgets => _widgets;
    public string WidgetAddress => $"http://127.0.0.1:{_widgetsModel.Port}/widget/<name>"; // what a widget's Copy URL gives



    // activator



    public static ConnectionsComponentViewModel ActivateTab(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager, ObsComponentModel obsModel, ObsManager obsManager,
        SpotifyComponentModel spotifyModel, SpotifyManager spotifyManager, WidgetsComponentModel widgetsModel, WidgetManager widgetManager,
        PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(chatbotModel, chatbotManager, audioModel, voicemeeterManager, obsModel, obsManager, spotifyModel, spotifyManager, widgetsModel, widgetManager);
    }



    // constructor



    public ConnectionsComponentViewModel(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager, ObsComponentModel obsModel, ObsManager obsManager,
        SpotifyComponentModel spotifyModel, SpotifyManager spotifyManager, WidgetsComponentModel widgetsModel, WidgetManager widgetManager)
    {
        _chatbotModel = chatbotModel;
        _chatbotManager = chatbotManager;
        _audioModel = audioModel;
        _voicemeeterManager = voicemeeterManager;
        _obsModel = obsModel;
        _obsManager = obsManager;
        _spotifyModel = spotifyModel;
        _spotifyManager = spotifyManager;
        _widgetsModel = widgetsModel;
        _widgetManager = widgetManager;

        _clientIdInput = _chatbotModel.ClientId;
        _clientSecretInput = _chatbotModel.ClientSecret;
        _obsAddressInput = _obsModel.Address;
        _obsPasswordInput = _obsModel.Password;
        _spotifyClientIdInput = _spotifyModel.ClientId;
        _widgetPortInput = _widgetsModel.Port.ToString();

        // twitch: the streamer listens to chat and events, the optional bot only sends chat
        // connecting (or retrying) first saves whatever is in the Client ID / Secret boxes
        _twitchStreamer = new("Streamer",
            () => (_chatbotManager.StreamerConnection, _chatbotManager.StatusMessage),
            () => { CommitCredentials(); _chatbotModel.Reconnect(); },
            () => _chatbotModel.SetIsEnabled(false),
            () => _chatbotModel.ConnectOnStartup,
            _chatbotModel.SetConnectOnStartup);

        _twitchBot = new("Bot",
            () => (_chatbotManager.BotConnection, _chatbotManager.BotStatusMessage),
            () => { CommitCredentials(); _chatbotModel.ReconnectBot(); },
            () => _chatbotModel.SetIsBotEnabled(false),
            () => _chatbotModel.BotConnectOnStartup,
            _chatbotModel.SetBotConnectOnStartup,
            "Chat only as bot",
            () => _chatbotModel.ChatOnlyAsBot,
            _chatbotModel.SetChatOnlyAsBot);

        _voicemeeter = new("Voicemeeter",
            () => (_voicemeeterManager.Status, _voicemeeterManager.Status == ConnectionStatus.Connected ? "Connected to Voicemeeter" : ""),
            _audioModel.ReconnectVoicemeeter,
            () => _audioModel.SetIsVoicemeeterEnabled(false),
            () => _audioModel.VoicemeeterConnectOnStartup,
            _audioModel.SetVoicemeeterConnectOnStartup);

        // obs: connecting (or retrying) first saves whatever is in the Address / Password boxes
        _obs = new("OBS",
            () => (_obsManager.Status, _obsManager.StatusMessage),
            () => { _obsModel.SetConnectionDetails(ObsAddressInput, ObsPasswordInput); _obsModel.Reconnect(); },
            () => _obsModel.SetIsEnabled(false),
            () => _obsModel.ConnectOnStartup,
            _obsModel.SetConnectOnStartup);

        // spotify: connecting (or retrying) first saves whatever is in the Client ID box
        _spotify = new("Spotify",
            () => (_spotifyManager.Status, _spotifyManager.StatusMessage),
            () => { _spotifyModel.SetClientId(SpotifyClientIdInput); _spotifyModel.Reconnect(); },
            () => _spotifyModel.SetIsEnabled(false),
            () => _spotifyModel.ConnectOnStartup,
            _spotifyModel.SetConnectOnStartup);

        // widgets: the server their pages (OBS's browser sources) come from; starting (or retrying) first takes the
        // Port box, a port that isn't one puts the saved one back
        _widgets = new("Server",
            () => (_widgetManager.Status, _widgetManager.StatusMessage),
            () => { CommitWidgetPort(); _widgetsModel.Restart(); },
            () => _widgetsModel.SetIsEnabled(false),
            () => _widgetsModel.StartOnStartup,
            _widgetsModel.SetStartOnStartup);

        _chatbotModel.CredentialsChanged += OnCredentialsChanged;
        _chatbotModel.Changed += OnTwitchChanged;
        _chatbotManager.StatusChanged += OnTwitchChanged;
        _audioModel.Changed += OnVoicemeeterChanged;
        _voicemeeterManager.StatusChanged += OnVoicemeeterChanged;
        _obsModel.Changed += OnObsChanged;
        _obsManager.StatusChanged += OnObsChanged;
        _spotifyModel.Changed += OnSpotifyChanged;
        _spotifyManager.StatusChanged += OnSpotifyChanged;
        _widgetsModel.Changed += OnWidgetsChanged;
        _widgetManager.StatusChanged += OnWidgetsChanged;
    }



    // model events



    private void OnCredentialsChanged()
    {
        ClientIdInput = _chatbotModel.ClientId;
        ClientSecretInput = _chatbotModel.ClientSecret;
    }



    private void OnTwitchChanged()
    {
        _twitchStreamer.Refresh();
        _twitchBot.Refresh();
    }



    private void OnVoicemeeterChanged() => _voicemeeter.Refresh();



    private void OnObsChanged() => _obs.Refresh();



    private void OnSpotifyChanged() => _spotify.Refresh();



    private void OnWidgetsChanged()
    {
        _widgets.Refresh();
        WidgetPortInput = _widgetsModel.Port.ToString();
        OnPropertyChanged(nameof(WidgetAddress));
    }



    // helpers



    private void CommitCredentials() => _chatbotModel.SetCredentials(ClientIdInput, ClientSecretInput);



    private void CommitWidgetPort()
    {
        if (!int.TryParse(WidgetPortInput.Trim(), out var port) || !_widgetsModel.TrySetPort(port)) WidgetPortInput = _widgetsModel.Port.ToString();
    }



    // disposal



    public override void Dispose()
    {
        _chatbotModel.CredentialsChanged -= OnCredentialsChanged;
        _chatbotModel.Changed -= OnTwitchChanged;
        _chatbotManager.StatusChanged -= OnTwitchChanged;
        _audioModel.Changed -= OnVoicemeeterChanged;
        _voicemeeterManager.StatusChanged -= OnVoicemeeterChanged;
        _obsModel.Changed -= OnObsChanged;
        _obsManager.StatusChanged -= OnObsChanged;
        _spotifyModel.Changed -= OnSpotifyChanged;
        _spotifyManager.StatusChanged -= OnSpotifyChanged;
        _widgetsModel.Changed -= OnWidgetsChanged;
        _widgetManager.StatusChanged -= OnWidgetsChanged;
    }
}
