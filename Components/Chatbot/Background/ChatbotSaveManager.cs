using System;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Chatbot.Background;



// the "twitchApp" section of Config/auth.json (git ignored, the secret is like a password)
public class TwitchAppData
{
    [JsonPropertyName("clientId")] public string ClientId { get; set; } = "";
    [JsonPropertyName("clientSecret")] public string ClientSecret { get; set; } = "";
}



// saves the chatbot model shortly after it changes, and once more when the app closes
// the chat block tree goes to chatbot.json, the app credentials to the shared Config/auth.json
public class ChatbotSaveManager : IDisposable
{
    // private



    private const string FileName = "chatbot";
    private const string CredentialsSection = "twitchApp";

    private readonly ChatbotComponentModel _model;
    private IDisposable? _pendingSave;



    // constructor



    public ChatbotSaveManager(ChatbotComponentModel model)
    {
        _model = model;
        _pendingSave = null;

        _model.Changed += OnModelChanged;
        _model.CredentialsChanged += OnModelChanged;
    }



    // model callbacks



    // typing in a text box changes the model on every key, so wait for it to settle
    private void OnModelChanged()
    {
        _pendingSave?.Dispose();
        _pendingSave = DispatcherTimer.RunOnce(SaveNow, TimeSpan.FromSeconds(1));
    }



    // public api



    public static ChatbotComponentModel Load()
    {
        var model = JsonFileStore.Load<ChatbotComponentModel>(FileName);
        var credentials = CredentialStore.Load<TwitchAppData>(CredentialsSection);

        model.SetCredentials(credentials.ClientId, credentials.ClientSecret);
        return model;
    }



    public void SaveNow()
    {
        _pendingSave?.Dispose();
        _pendingSave = null;

        JsonFileStore.Save(FileName, _model);
        CredentialStore.Save(CredentialsSection, new TwitchAppData { ClientId = _model.ClientId, ClientSecret = _model.ClientSecret });
    }



    // disposal



    public void Dispose()
    {
        _model.Changed -= OnModelChanged;
        _model.CredentialsChanged -= OnModelChanged;

        // the app is closing, write the final state instead of dropping a pending save
        if (_pendingSave != null) SaveNow();
    }
}
