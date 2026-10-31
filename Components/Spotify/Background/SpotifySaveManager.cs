using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Spotify.Background;



// the "spotifyApp" section of Config/auth.json
public class SpotifyAppData
{
    [JsonPropertyName("clientId")] public string ClientId { get; set; } = "";
}



// saves the Spotify settings whenever one changes (they change rarely, so no delay): connect on startup to
// Config/spotify.json, the client id to Config/auth.json
public class SpotifySaveManager : IDisposable
{
    // private



    private const string FileName = "spotify";
    private const string CredentialsSection = "spotifyApp";
    private readonly SpotifyComponentModel _model;



    // constructor



    public SpotifySaveManager(SpotifyComponentModel model)
    {
        _model = model;
        _model.Changed += OnModelChanged;
    }



    // public api



    public static SpotifyComponentModel Load()
    {
        var model = JsonFileStore.LoadOrReset<SpotifyComponentModel>(FileName);
        model.LoadClientId(CredentialStore.Load<SpotifyAppData>(CredentialsSection).ClientId);
        return model;
    }



    // model callbacks



    private void OnModelChanged()
    {
        JsonFileStore.Save(FileName, _model);
        CredentialStore.Save(CredentialsSection, new SpotifyAppData { ClientId = _model.ClientId });
    }



    // disposal



    public void Dispose() => _model.Changed -= OnModelChanged;
}
