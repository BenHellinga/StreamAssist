using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Spotify.Actions;



public class AddToSpotifyPlaylistAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Add to Spotify Playlist";
    [JsonIgnore] public override string Description => "Adds a song to the end of a playlist: one of yours by name, or its link. Song is a link or words to search for (the first song found), empty adds the song playing now. A song already in the playlist isn't added again.";
    [JsonIgnore] public override string? Subgroup => "Songs";



    // private



    private readonly ValueSlot<string> _playlist;
    private readonly ValueSlot<string> _song;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("playlist")]
    private ValueSlotData JsonPlaylist
    {
        get => _playlist.GetData();
        set => _playlist.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("song")]
    private ValueSlotData JsonSong
    {
        get => _song.GetData();
        set => _song.LoadData(value);
    }



    // constructor



    public AddToSpotifyPlaylistAction()
    {
        _playlist = new("Playlist");
        _song = new("Song");
        _slots = [_playlist, _song];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var playlist = await _playlist.EvaluateAsync(context);
        var song = await _song.EvaluateAsync(context);
        await Api(context).Spotify.AddToPlaylistAsync(playlist, song);
    }
}
