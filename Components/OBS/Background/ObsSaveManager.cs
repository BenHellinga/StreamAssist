using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.OBS.Background;



// the "obs" section of Config/auth.json (git ignored, it's a password)
public class ObsCredentialsData
{
    [JsonPropertyName("password")] public string Password { get; set; } = "";
}



// saves the OBS settings whenever one changes (they change rarely, so no delay): the address and connect on startup to
// Config/obs.json, the password to Config/auth.json
public class ObsSaveManager : IDisposable
{
    // private



    private const string FileName = "obs";
    private const string CredentialsSection = "obs";
    private readonly ObsComponentModel _model;



    // constructor



    public ObsSaveManager(ObsComponentModel model)
    {
        _model = model;
        _model.Changed += OnModelChanged;
    }



    // public api



    public static ObsComponentModel Load()
    {
        var model = JsonFileStore.LoadOrReset<ObsComponentModel>(FileName);
        model.LoadPassword(CredentialStore.Load<ObsCredentialsData>(CredentialsSection).Password);
        return model;
    }



    // model callbacks



    private void OnModelChanged()
    {
        JsonFileStore.Save(FileName, _model);
        CredentialStore.Save(CredentialsSection, new ObsCredentialsData { Password = _model.Password });
    }



    // disposal



    public void Dispose() => _model.Changed -= OnModelChanged;
}
