using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core.Configurator;
using NAudio.Wave;

namespace StreamAssist.AppActions;



public class PlaySoundAction : ActionModel
{
    // info



    [JsonIgnore] public override string Name => "Play Sound";
    [JsonIgnore] public override string Description => "Plays the configured .mp3/.wav file to StreamAssistAudioOut.";



    // private



    private string _filepath;

    // public



    [ConfigurableParameter("Filepath", ConfiguratorType.Filepath)] [JsonPropertyName("filepath")]
    public string Filepath
    {
        get => _filepath;
        set => SetParameter(ref _filepath, value);
    }



    // constructor



    public PlaySoundAction()
    {
        _filepath = "";
    }



    // execute



    public override Task SafeExecuteAsync()
    {
        if (_filepath == "") return Task.CompletedTask;

        var audioFile = new AudioFileReader(_filepath);
        var outputDevice = new WaveOutEvent();

        outputDevice.Init(audioFile);
        outputDevice.Play();

        outputDevice.PlaybackStopped += (_, _) =>
        {
            outputDevice.Dispose();
            audioFile.Dispose();
        };
        
        return Task.CompletedTask;
    }
}