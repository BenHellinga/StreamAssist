using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsVolumeAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Volume";
    [JsonIgnore] public override string Description => "Sets an audio source's volume in dB, like the OBS mixer: 0 is full volume, lower is quieter (-100 to 26).";



    // private



    private readonly ValueSlot<string> _source;
    private string _mode;
    private readonly ValueSlot<double> _volume;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // Set to the value, or Change by it (added to what it is now, e.g. -2 for a step down)
    [ConfigurableParameter("Mode", ConfiguratorType.Menu)] [JsonPropertyName("mode")]
    public string Mode
    {
        get => _mode;
        set => SetParameter(ref _mode, value);
    }

    public static List<string> GetModeMenuItems() => ["Set to", "Change by"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("volume")]
    private ValueSlotData JsonVolume
    {
        get => _volume.GetData();
        set => _volume.LoadData(value);
    }



    // constructor



    public SetObsVolumeAction()
    {
        _source = new("Source");
        _mode = "Set to";
        _volume = new("Volume dB");
        _slots = [_source, _volume];
        _volume.SetText("0");

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var volume = await _volume.EvaluateAsync(context);

        if (_mode == "Change by")
        {
            var (ok, now) = await obs.TryGetVolumeAsync(source);
            if (!ok) throw new BlockFailedException("volume not read");
            volume += now;
        }

        if (!(await obs.TrySetVolumeAsync(source, volume)).Ok) throw new BlockFailedException("volume not set");
    }
}
