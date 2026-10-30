using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsMuteAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Mute";
    [JsonIgnore] public override string Description => "Mutes, unmutes or toggles an audio source in OBS.";



    // private



    private string _change;
    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Do", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["Mute", "Unmute", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }



    // constructor



    public SetObsMuteAction()
    {
        _change = "Toggle";
        _source = new("Source");
        _slots = [_source];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var change = _change == "Mute" ? ObsToggle.On : _change == "Unmute" ? ObsToggle.Off : ObsToggle.Toggle;

        if (!(await obs.TrySetMutedAsync(source, change)).Ok) throw new BlockFailedException("mute not changed");
    }
}
