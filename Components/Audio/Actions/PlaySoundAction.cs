using System;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Audio.Actions;



public class PlaySoundAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Play Sound";
    [JsonIgnore] public override string Description => "Plays an .mp3 or .wav file. A folder (e.g. Sounds/hype) plays one of its sounds at random. Volume is 0 to 1 (kept to that range), typed or from a block. With no Dock it plays straight away, over anything else, and can't be stopped; naming a sound dock queues it there, to play after the sounds before it, and it can be skipped or stopped.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.App;



    // private



    private string _filepath;
    private readonly ValueSlot<double> _volume;
    private readonly ValueSlot<string> _dock;
    private readonly MenuSlot _outputDevice;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Filepath", ConfiguratorType.Filepath)] [JsonPropertyName("filepath")]
    public string Filepath
    {
        get => _filepath;
        set => SetParameter(ref _filepath, value);
    }


    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;
    [JsonIgnore] public override int SlotsBeforeSettings => 0; // the file first



    // json



    [JsonInclude] [JsonPropertyName("outputDevice")]
    private ValueSlotData JsonOutputDevice
    {
        get => _outputDevice.GetData();
        set => _outputDevice.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("volume")]
    private ValueSlotData JsonVolume
    {
        get => _volume.GetData();
        set => _volume.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("dock")]
    private ValueSlotData JsonDock
    {
        get => _dock.GetData();
        set => _dock.LoadData(value);
    }



    // constructor



    public PlaySoundAction()
    {
        _filepath = "";
        _volume = new("Volume");
        _outputDevice = new("Output Device", GetOutputDeviceMenuItems, "Default", isFixed: false);
        _dock = new("Dock");
        _slots = [_outputDevice, _volume, _dock];

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
        var outputDevice = await _outputDevice.EvaluateAsync(context);
        var volume = await _volume.EvaluateAsync(context);
        if (double.IsNaN(volume)) throw Fail("volume isn't a number");

        var dashboard = GlobalContext.DashboardComponentModel;
        var dockName = await _dock.EvaluateAsync(context);
        SoundsModel? queue = null;
        if (dockName.Trim() != "" && !(dashboard ?? throw Fail("no dashboard")).TryFindSoundQueue(dockName, out queue, out var dockError))
            throw Fail(dockError);

        if (queue == null)
        {
            AudioHelper.TryPlaySound(_filepath, (float)Math.Clamp(volume, 0, 1), outputDevice);
            return;
        }

        if (AudioHelper.TryOpenSound(_filepath, (float)Math.Clamp(volume, 0, 1), outputDevice) is { } playback)
            queue.Enqueue(System.IO.Path.GetFileNameWithoutExtension(_filepath.TrimEnd('/', '\\')), Task.FromResult<ISoundPlayback?>(playback));
    }
}