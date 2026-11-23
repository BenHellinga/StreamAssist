using StreamAssist.Scripting;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.OBS.Actions;



public class SetObsMuteAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set OBS Mute";
    [JsonIgnore] public override string Description => "Mutes, unmutes or toggles an audio source in OBS.";
    [JsonIgnore] public override string? Subgroup => "Audio";



    // private



    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _source;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Mute", "Unmute", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("source")]
    private ValueSlotData JsonSource
    {
        get => _source.GetData();
        set => _source.LoadData(value);
    }



    // constructor



    public SetObsMuteAction()
    {
        _change = new("Do", GetChangeMenuItems, "Toggle");
        _source = new("Source");
        _slots = [_change, _source];

        TrackSlots();
    }



    // execute



    // OBS requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        var source = await _source.EvaluateAsync(context);
        await Api(context).Obs.SetMutedAsync(source, change == "Mute" ? Switch.On : change == "Unmute" ? Switch.Off : Switch.Toggle);
    }
}
