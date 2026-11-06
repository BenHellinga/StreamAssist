using System;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using StreamAssist.Components.Audio.Background;

namespace StreamAssist.Components.Audio.Actions;



public class PlaySoundAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Play Sound";
    [JsonIgnore] public override string Description => "Plays an .mp3 or .wav file. A folder (e.g. Sounds/hype) plays one of its sounds at random. Volume is 0 to 1 (kept to that range), typed or from a block.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.App;



    // private



    private string _filepath;
    private readonly ValueSlot<double> _volume;
    private string _outputDevice;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Filepath", ConfiguratorType.Filepath)] [JsonPropertyName("filepath")]
    public string Filepath
    {
        get => _filepath;
        set => SetParameter(ref _filepath, value);
    }

    [ConfigurableParameter("Output Device", ConfiguratorType.Menu)] [JsonPropertyName("outputDevice")]
    public string OutputDevice
    {
        get => _outputDevice;
        set => SetParameter(ref _outputDevice, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("volume")]
    private ValueSlotData JsonVolume
    {
        get => _volume.GetData();
        set => _volume.LoadData(value);
    }



    // constructor



    public PlaySoundAction()
    {
        _filepath = "";
        _volume = new("Volume");
        _outputDevice = "Default";
        _slots = [_volume];

        _volume.SetText("1");

        TrackSlots();
    }



    // public



    public static List<string> GetOutputDeviceMenuItems()
    {
        var list = new List<string>() { "Default" };
        list.AddRange(AudioHelper.GetDeviceNames());
        return list;
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var volume = await _volume.EvaluateAsync(context);
        if (double.IsNaN(volume)) throw Fail("volume isn't a number");

        AudioHelper.TryPlaySound(_filepath, (float)Math.Clamp(volume, 0, 1), _outputDevice);
    }
}