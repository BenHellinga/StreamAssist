using StreamAssist.Scripting;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Spotify.Actions;



public class SetSpotifyShuffleAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Spotify Shuffle";
    [JsonIgnore] public override string Description => "Turns Spotify's shuffle on or off, or toggles it. Needs Spotify Premium.";
    [JsonIgnore] public override string? Subgroup => "Playback";



    // private



    private readonly MenuSlot _change;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }



    // constructor



    public SetSpotifyShuffleAction()
    {
        _change = new("Do", GetChangeMenuItems, "Toggle");
        _slots = [_change];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        await Api(context).Spotify.SetShuffleAsync(change == "On" ? Switch.On : change == "Off" ? Switch.Off : Switch.Toggle);
    }
}
