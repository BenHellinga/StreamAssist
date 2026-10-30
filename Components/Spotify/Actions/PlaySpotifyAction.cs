using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class PlaySpotifyAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Play Spotify";
    [JsonIgnore] public override string Description => "Plays a song, or a playlist or album from its start. What is a Spotify link (Share > Copy link), or words: Find picks what they find (the first song or album a search finds, or one of your playlists by name). Parameters work, e.g. {rest} from a chat command or {input} from a redeem. If nothing is playing it starts on the computer the Spotify app is open on. Needs Spotify Premium.";



    // private



    private string _find;
    private readonly ValueSlot<string> _what;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Find", ConfiguratorType.Menu)] [JsonPropertyName("find")]
    public string Find
    {
        get => _find;
        set => SetParameter(ref _find, value);
    }

    public static List<string> GetFindMenuItems() => ["Song", "Playlist", "Album"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("what")]
    private ValueSlotData JsonWhat
    {
        get => _what.GetData();
        set => _what.LoadData(value);
    }



    // constructor



    public PlaySpotifyAction()
    {
        _find = "Song";
        _what = new("What");
        _slots = [_what];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        var what = await _what.EvaluateAsync(context);
        if (what.Trim() == "") throw Fail("nothing to play");
        var find = _find == "Playlist" ? SpotifyFind.Playlist : _find == "Album" ? SpotifyFind.Album : SpotifyFind.Song;

        if (!await spotify.TryPlayAsync(what, find, context.CancellationToken)) throw new BlockFailedException("not played");
    }
}
