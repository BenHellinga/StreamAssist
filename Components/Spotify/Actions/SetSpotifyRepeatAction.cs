using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class SetSpotifyRepeatAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Spotify Repeat";
    [JsonIgnore] public override string Description => "Sets Spotify's repeat: Off, Song (repeat the song) or Playlist (repeat the playlist or album). Needs Spotify Premium.";
    [JsonIgnore] public override string? Subgroup => "Playback";



    // private



    private readonly MenuSlot _repeat;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetRepeatMenuItems() => ["Off", "Song", "Playlist"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("repeat")]
    private ValueSlotData JsonRepeat
    {
        get => _repeat.GetData();
        set => _repeat.LoadData(value);
    }



    // constructor



    public SetSpotifyRepeatAction()
    {
        _repeat = new("Repeat", GetRepeatMenuItems, "Off");
        _slots = [_repeat];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var repeat = await _repeat.EvaluateAsync(context);
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        if (!await spotify.TrySetRepeatAsync(repeat, context.CancellationToken)) throw new BlockFailedException("repeat not set");
    }
}
