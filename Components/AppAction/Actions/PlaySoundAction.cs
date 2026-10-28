using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using StreamAssist.Components.Audio.Background;

namespace StreamAssist.Components.AppAction.Actions;



public class PlaySoundAction : ActionModel
{
    // info



    [JsonIgnore] public override string Name => "Play Sound";
    [JsonIgnore] public override string Description => "Plays the configured .mp3/.wav file.";



    // private



    private string _filepath;
    private float _volume;
    private string _outputDevice;



    // public



    [ConfigurableParameter("Filepath", ConfiguratorType.Filepath)] [JsonPropertyName("filepath")]
    public string Filepath
    {
        get => _filepath;
        set => SetParameter(ref _filepath, value);
    }

    [ConfigurableParameter("Volume", ConfiguratorType.Slider)] [JsonPropertyName("volume")]
    public float Volume
    {
        get => _volume;
        set => SetParameter(ref _volume, value);
    }

    [ConfigurableParameter("Output Device", ConfiguratorType.Menu)] [JsonPropertyName("outputDevice")]
    public string OutputDevice
    {
        get => _outputDevice;
        set => SetParameter(ref _outputDevice, value);
    }



    // constructor



    public PlaySoundAction()
    {
        _filepath = "";
        _volume = 1f;
        _outputDevice = "Default";
    }



    // public



    public static (double, double) GetVolumeBounds() => (0, 1);



    public static List<string> GetOutputDeviceMenuItems()
    {
        var list = new List<string>() { "Default" };
        list.AddRange(AudioHelper.GetDeviceNames());
        return list;
    }



    // execute



    protected override void Execute()
    {
        AudioHelper.TryPlaySound(_filepath, _volume, _outputDevice);
    }
}