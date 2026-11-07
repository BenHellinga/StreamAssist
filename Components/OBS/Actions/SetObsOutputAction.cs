using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsOutputAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Output";
    [JsonIgnore] public override string Description => "Starts, stops or toggles recording, streaming, the replay buffer or the virtual camera. Starting one that's already running does nothing.";
    [JsonIgnore] public override string? Subgroup => "Outputs";



    // private



    private readonly MenuSlot _output;
    private readonly MenuSlot _change;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetOutputMenuItems() => ObsManager.Outputs.ToList();


    public static List<string> GetChangeMenuItems() => ["Start", "Stop", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("output")]
    private ValueSlotData JsonOutput
    {
        get => _output.GetData();
        set => _output.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }



    // constructor



    public SetObsOutputAction()
    {
        _output = new("Output", GetOutputMenuItems, "Recording");
        _change = new("Change", GetChangeMenuItems, "Toggle");
        _slots = [_output, _change];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var output = await _output.EvaluateAsync(context);
        var changeOption = await _change.EvaluateAsync(context);
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var change = changeOption == "Start" ? ObsToggle.On : changeOption == "Stop" ? ObsToggle.Off : ObsToggle.Toggle;

        if (!(await obs.TrySetOutputAsync(output, change)).Ok) throw new BlockFailedException("output not changed");
    }
}
