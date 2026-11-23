using StreamAssist.Scripting;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Spotify.Actions;



public class PlaySpotifyAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Play Spotify";
    [JsonIgnore] public override string Description => "Plays a song, or a playlist or album from its start. What is a Spotify link (Share > Copy link), or words: Find picks what they find (the first song or album a search finds, or one of your playlists by name). Parameters work, e.g. {rest} from a chat command or {input} from a redeem. If nothing is playing it starts on the computer the Spotify app is open on. Needs Spotify Premium.";
    [JsonIgnore] public override string? Subgroup => "Playback";



    // private



    private readonly MenuSlot _find;
    private readonly ValueSlot<string> _what;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetFindMenuItems() => ["Song", "Playlist", "Album"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("find")]
    private ValueSlotData JsonFind
    {
        get => _find.GetData();
        set => _find.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("what")]
    private ValueSlotData JsonWhat
    {
        get => _what.GetData();
        set => _what.LoadData(value);
    }



    // constructor



    public PlaySpotifyAction()
    {
        _find = new("Find", GetFindMenuItems, "Song");
        _what = new("What");
        _slots = [_find, _what];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var find = await _find.EvaluateAsync(context);
        var what = await _what.EvaluateAsync(context);
        await Api(context).Spotify.PlayAsync(what, find == "Playlist" ? SpotifyKind.Playlist : find == "Album" ? SpotifyKind.Album : SpotifyKind.Song);
    }
}
