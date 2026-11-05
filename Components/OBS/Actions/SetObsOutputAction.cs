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



    // private



    private string _output;
    private string _change;



    // public



    [ConfigurableParameter("Output", ConfiguratorType.Menu)] [JsonPropertyName("output")]
    public string Output
    {
        get => _output;
        set => SetParameter(ref _output, value);
    }

    public static List<string> GetOutputMenuItems() => ObsManager.Outputs.ToList();

    [ConfigurableParameter("Change", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["Start", "Stop", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => [];



    // constructor



    public SetObsOutputAction()
    {
        _output = "Recording";
        _change = "Toggle";
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");
        var change = _change == "Start" ? ObsToggle.On : _change == "Stop" ? ObsToggle.Off : ObsToggle.Toggle;

        if (!(await obs.TrySetOutputAsync(_output, change)).Ok) throw new BlockFailedException("output not changed");
    }
}
