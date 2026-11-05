using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class SetSpotifyVolumeAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Spotify Volume";
    [JsonIgnore] public override string Description => "Sets Spotify's volume, 0 to 100 (kept in that range). Some devices (e.g. phones) don't let it be changed. Needs Spotify Premium.";



    // private



    private string _mode;
    private readonly ValueSlot<int> _volume;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // Set to the value, or Change by it (added to what it is now, e.g. -2 for a step down)
    [ConfigurableParameter("Mode", ConfiguratorType.Menu)] [JsonPropertyName("mode")]
    public string Mode
    {
        get => _mode;
        set => SetParameter(ref _mode, value);
    }

    public static List<string> GetModeMenuItems() => ["Set to", "Change by"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("volume")]
    private ValueSlotData JsonVolume
    {
        get => _volume.GetData();
        set => _volume.LoadData(value);
    }



    // constructor



    public SetSpotifyVolumeAction()
    {
        _mode = "Set to";
        _volume = new("Volume");
        _slots = [_volume];

        _volume.SetText("50");

        TrackSlots();
    }



    // execute



    // spotify requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        var volume = await _volume.EvaluateAsync(context);

        // Spotify may report the old volume for a moment after a change, so quick presses can land on the same step
        if (_mode == "Change by")
        {
            var (ok, now) = await spotify.TryGetPlaybackAsync(SpotifyInfos.Find(SpotifyInfos.Playback, "Volume")!, context.CancellationToken);
            if (!ok) throw new BlockFailedException("volume not read");
            volume += now is int percent ? percent : 0;
        }

        if (!await spotify.TrySetVolumeAsync(volume, context.CancellationToken)) throw new BlockFailedException("volume not set");
    }
}
