using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Blocks;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.Model;



// everything the chatbot is configured with: the twitch app credentials, whether to connect, and the chat block tree
// the credentials are saved to Config/auth.json by ChatbotSaveManager, the rest to chatbot.json
public class ChatbotComponentModel : ModelBase
{
    // private



    private string _clientId;
    private string _clientSecret;
    private bool _isEnabled;
    private readonly BlockSlot _root;



    // public



    // the app registered on dev.twitch.tv, both accounts log in through it (treat the secret like a password)
    [JsonIgnore] public string ClientId => _clientId;
    [JsonIgnore] public string ClientSecret => _clientSecret;

    [JsonIgnore] public bool IsEnabled => _isEnabled; // whether the streamer account should be connected
    [JsonIgnore] public BlockSlot Root => _root; // the one chat block every message starts at



    // json



    [JsonInclude] [JsonPropertyName("isEnabled")]
    private bool JsonIsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }

    [JsonInclude] [JsonPropertyName("rootBlock")]
    private PendingBlockData? JsonRoot
    {
        get => _root.GetData();
        set => _root.LoadData(value);
    }



    // events



    public event Action? ConnectionChanged; // credentials or enabled changed
    public event Action? CredentialsChanged; // client id or secret changed (saved to auth.json)
    public event Action? Changed; // anything else changed (saved to chatbot.json)



    // constructor



    public ChatbotComponentModel()
    {
        _clientId = "";
        _clientSecret = "";
        _isEnabled = false;
        _root = new("", typeof(ChatBlock));
        _root.TrySetBlock(new MatchTextChatBlock()); // a fresh chatbot starts with an empty Match Text

        _root.Changed += OnRootChanged;
    }



    // connection settings



    public void SetCredentials(string clientId, string clientSecret)
    {
        var id = clientId.Trim();
        var secret = clientSecret.Trim();
        if (id == _clientId && secret == _clientSecret) return;

        _clientId = id;
        _clientSecret = secret;

        CredentialsChanged?.Invoke();
        ConnectionChanged?.Invoke();
    }



    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled == _isEnabled) return;

        _isEnabled = isEnabled;
        ConnectionChanged?.Invoke();
        Changed?.Invoke();
    }



    // helpers



    private void OnRootChanged() => Changed?.Invoke();



    // disposal



    public override void Dispose()
    {
        _root.Changed -= OnRootChanged;
        _root.Dispose();
    }
}
