using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
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
    private readonly ConnectionRowViewModel _twitchStreamer;
    private readonly ConnectionRowViewModel _twitchBot;
    private readonly ConnectionRowViewModel _voicemeeter;
    private readonly ConnectionRowViewModel _obs;
    private readonly ConnectionRowViewModel _spotify;

    [ObservableProperty] private string _clientIdInput;
    [ObservableProperty] private string _clientSecretInput;
    [ObservableProperty] private string _obsAddressInput;
    [ObservableProperty] private string _obsPasswordInput;
    [ObservableProperty] private string _spotifyClientIdInput;



    // public



    public ConnectionRowViewModel TwitchStreamer => _twitchStreamer;
    public ConnectionRowViewModel TwitchBot => _twitchBot;
    public ConnectionRowViewModel Voicemeeter => _voicemeeter;
    public ConnectionRowViewModel Obs => _obs;
    public ConnectionRowViewModel Spotify => _spotify;
    public string SpotifyRedirectUri => SpotifyComponentModel.RedirectUri;



    // activator



    public static ConnectionsComponentViewModel ActivateTab(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager, ObsComponentModel obsModel, ObsManager obsManager,
        SpotifyComponentModel spotifyModel, SpotifyManager spotifyManager, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(chatbotModel, chatbotManager, audioModel, voicemeeterManager, obsModel, obsManager, spotifyModel, spotifyManager);
    }



    // constructor



    public ConnectionsComponentViewModel(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager, ObsComponentModel obsModel, ObsManager obsManager,
        SpotifyComponentModel spotifyModel, SpotifyManager spotifyManager)
    {
        _chatbotModel = chatbotModel;
        _chatbotManager = chatbotManager;
        _audioModel = audioModel;
        _voicemeeterManager = voicemeeterManager;
        _obsModel = obsModel;
        _obsManager = obsManager;
        _spotifyModel = spotifyModel;
        _spotifyManager = spotifyManager;

        _clientIdInput = _chatbotModel.ClientId;
        _clientSecretInput = _chatbotModel.ClientSecret;
        _obsAddressInput = _obsModel.Address;
        _obsPasswordInput = _obsModel.Password;
        _spotifyClientIdInput = _spotifyModel.ClientId;

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

        _chatbotModel.CredentialsChanged += OnCredentialsChanged;
        _chatbotModel.Changed += OnTwitchChanged;
        _chatbotManager.StatusChanged += OnTwitchChanged;
        _audioModel.Changed += OnVoicemeeterChanged;
        _voicemeeterManager.StatusChanged += OnVoicemeeterChanged;
        _obsModel.Changed += OnObsChanged;
        _obsManager.StatusChanged += OnObsChanged;
        _spotifyModel.Changed += OnSpotifyChanged;
        _spotifyManager.StatusChanged += OnSpotifyChanged;
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



    // helpers



    private void CommitCredentials() => _chatbotModel.SetCredentials(ClientIdInput, ClientSecretInput);



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
    }
}
