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



    private string _change;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Do", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["Pause", "Resume", "Toggle", "Next", "Previous"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public SetSpotifyPlaybackAction()
    {
        _change = "Toggle";
        _slots = [];

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        if (!await spotify.TrySetPlaybackAsync(_change, context.CancellationToken)) throw new BlockFailedException("playback not changed");
    }
}
