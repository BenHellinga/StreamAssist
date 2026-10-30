using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Actions;



public class SetSpotifyVolumeAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Spotify Volume";
    [JsonIgnore] public override string Description => "Sets Spotify's volume, 0 to 100 (kept in that range). Some devices (e.g. phones) don't let it be changed. Needs Spotify Premium.";



    // private



    private readonly ValueSlot<int> _volume;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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

        if (!await spotify.TrySetVolumeAsync(volume, context.CancellationToken)) throw new BlockFailedException("volume not set");
    }
}
