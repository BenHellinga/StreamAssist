using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Actions;



public class PlayMp3Action : IAppAction
{
    // private storage fields
    private string? _filepath;

    // public getter properties
    [JsonIgnore] public string Name => "Play .mp3";
    [JsonIgnore] public string Description => "Plays the configured .mp3 file to StreamAssistAudioOut.";
    [JsonIgnore] public string? Filepath => _filepath;



    // json



    [JsonInclude] [JsonPropertyName("filepath")]
    private string? JsonFilepath
    {
        get => _filepath;
        set => _filepath = value;
    }



    // constructor



    public PlayMp3Action()
    {
        _filepath = null;
    }



    // execute



    public Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"filepath: '{_filepath}'");
        return Task.CompletedTask;
    }
}