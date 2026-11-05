using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsFilterAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Filter";
    [JsonIgnore] public override string Description => "Turns a filter on a source (or a scene) on, off or toggles it, both by their names as OBS shows them.";



    // private



    private string _change;
    private readonly ValueSlot<string> _source;
    private readonly ValueSlot<string> _filter;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Change", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("filter")]
    private ValueSlotData JsonFilter
    {
        get => _filter.GetData();
        set => _filter.LoadData(value);
    }



    // constructor



    public SetObsFilterAction()
    {
        _change = "Toggle";
        _source = new("Source");
        _filter = new("Filter");
        _slots = [_source, _filter];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var source = await _source.EvaluateAsync(context);
        var filter = await _filter.EvaluateAsync(context);
        var change = _change == "On" ? ObsToggle.On : _change == "Off" ? ObsToggle.Off : ObsToggle.Toggle;

        if (!(await obs.TrySetFilterEnabledAsync(source, filter, change)).Ok) throw new BlockFailedException("filter not changed");
    }
}
