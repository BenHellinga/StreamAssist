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



    // private



    private string _repeat;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Repeat", ConfiguratorType.Menu)] [JsonPropertyName("repeat")]
    public string Repeat
    {
        get => _repeat;
        set => SetParameter(ref _repeat, value);
    }

    public static List<string> GetRepeatMenuItems() => ["Off", "Song", "Playlist"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public SetSpotifyRepeatAction()
    {
        _repeat = "Off";
        _slots = [];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        if (!await spotify.TrySetRepeatAsync(_repeat, context.CancellationToken)) throw new BlockFailedException("repeat not set");
    }
}
