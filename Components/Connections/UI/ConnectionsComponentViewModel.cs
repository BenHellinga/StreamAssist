using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
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
    private readonly ConnectionRowViewModel _twitchStreamer;
    private readonly ConnectionRowViewModel _twitchBot;
    private readonly ConnectionRowViewModel _voicemeeter;

    [ObservableProperty] private string _clientIdInput;
    [ObservableProperty] private string _clientSecretInput;



    // public



    public ConnectionRowViewModel TwitchStreamer => _twitchStreamer;
    public ConnectionRowViewModel TwitchBot => _twitchBot;
    public ConnectionRowViewModel Voicemeeter => _voicemeeter;



    // activator



    public static ConnectionsComponentViewModel ActivateTab(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(null);

        return new(chatbotModel, chatbotManager, audioModel, voicemeeterManager);
    }



    // constructor



    public ConnectionsComponentViewModel(ChatbotComponentModel chatbotModel, ChatbotManager chatbotManager,
        AudioComponentModel audioModel, VoicemeeterManager voicemeeterManager)
    {
        _chatbotModel = chatbotModel;
        _chatbotManager = chatbotManager;
        _audioModel = audioModel;
        _voicemeeterManager = voicemeeterManager;

        _clientIdInput = _chatbotModel.ClientId;
        _clientSecretInput = _chatbotModel.ClientSecret;

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
            _chatbotModel.SetBotConnectOnStartup);

        _voicemeeter = new("Voicemeeter",
            () => (_voicemeeterManager.Status, ""),
            _audioModel.ReconnectVoicemeeter,
            () => _audioModel.SetIsVoicemeeterEnabled(false),
            () => _audioModel.VoicemeeterConnectOnStartup,
            _audioModel.SetVoicemeeterConnectOnStartup);

        _chatbotModel.CredentialsChanged += OnCredentialsChanged;
        _chatbotModel.Changed += OnTwitchChanged;
        _chatbotManager.StatusChanged += OnTwitchChanged;
        _audioModel.Changed += OnVoicemeeterChanged;
        _voicemeeterManager.StatusChanged += OnVoicemeeterChanged;
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
    }
}
