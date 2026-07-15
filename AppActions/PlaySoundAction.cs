using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core.Configurator;

namespace StreamAssist.AppActions;



public class PlaySoundAction : AppActionBase
{
    // action info
    [JsonIgnore] public override string Name => "Play Sound";
    [JsonIgnore] public override string Description => "Plays the configured .mp3/.wav file to StreamAssistAudioOut.";

    // private storage field
    private string? _filepath;

    // public getter properties
    [ConfigurableParameter("Sound File", ConfiguratorType.Filepath)] [JsonPropertyName("filepath")]
    public string? Filepath
    {
        get => _filepath;
        set => SetParameter(ref _filepath, value);
    }



    // constructor



    public PlaySoundAction()
    {
        _filepath = null;
    }



    // execute



    public override Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"filepath: '{_filepath}'");
        return Task.CompletedTask;
    }
}