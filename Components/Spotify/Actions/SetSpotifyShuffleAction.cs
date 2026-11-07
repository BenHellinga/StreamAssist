using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

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
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        bool? on = change == "On" ? true : change == "Off" ? false : null;

        if (!await spotify.TrySetShuffleAsync(on, context.CancellationToken)) throw new BlockFailedException("shuffle not set");
    }
}
