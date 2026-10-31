using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class QueueSpotifySongAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Queue Spotify Song";
    [JsonIgnore] public override string Description => "Adds a song to Spotify's queue, to play after the current one. Song is a link (Share > Copy link) or words to search for (the first song found), e.g. {rest} for a song request command. Needs Spotify Premium.";



    // private



    private readonly ValueSlot<string> _song;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("song")]
    private ValueSlotData JsonSong
    {
        get => _song.GetData();
        set => _song.LoadData(value);
    }



    // constructor



    public QueueSpotifySongAction()
    {
        _song = new("Song");
        _slots = [_song];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        var song = await _song.EvaluateAsync(context);
        if (song.Trim() == "") throw Fail("no song given");

        if (!await spotify.TryQueueAsync(song, context.CancellationToken)) throw new BlockFailedException("not queued");
    }
}
