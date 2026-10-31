using System;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Audio.Background;



// saves the audio settings to Config/audio.json whenever one changes (they change rarely, so no delay)
public class AudioSaveManager : IDisposable
{
    // private



    private const string FileName = "audio";

    private readonly AudioComponentModel _model;



    // constructor



    public AudioSaveManager(AudioComponentModel model)
    {
        _model = model;
        _model.Changed += OnModelChanged;
    }



    // public api



    public static AudioComponentModel Load() => JsonFileStore.LoadOrReset<AudioComponentModel>(FileName);



    // model callbacks



    private void OnModelChanged() => JsonFileStore.Save(FileName, _model);



    // disposal



    public void Dispose() => _model.Changed -= OnModelChanged;
}
