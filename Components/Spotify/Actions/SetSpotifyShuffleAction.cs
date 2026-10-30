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

    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public SetSpotifyShuffleAction()
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
        bool? on = _change == "On" ? true : _change == "Off" ? false : null;

        if (!await spotify.TrySetShuffleAsync(on, context.CancellationToken)) throw new BlockFailedException("shuffle not set");
    }
}
