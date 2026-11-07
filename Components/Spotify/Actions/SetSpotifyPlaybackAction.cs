using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class SetSpotifyPlaybackAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Spotify Playback";
    [JsonIgnore] public override string Description => "Pauses, resumes or toggles Spotify, or skips to the next or previous song. Pausing while paused (or resuming while playing) does nothing. Needs Spotify Premium.";



    // private



    private readonly MenuSlot _change;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Pause", "Resume", "Toggle", "Next", "Previous"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }



    // constructor



    public SetSpotifyPlaybackAction()
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
        if (!await spotify.TrySetPlaybackAsync(change, context.CancellationToken)) throw new BlockFailedException("playback not changed");
    }
}
