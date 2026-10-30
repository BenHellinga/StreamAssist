using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Values;



public class GetSpotifyPlaybackBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Spotify Playback";
    [JsonIgnore] public override string Description => "Reads what Spotify is playing: Title, Artist, Album, Link, Playing, Progress Seconds, Length Seconds, Volume, Shuffle or Repeat. Picking one picks its type. With nothing playing anywhere it reads as not playing, empty text and 0.";



    // private



    private string _setting;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // picking a setting also picks its type below (when the slot it's in can take it)
    [ConfigurableParameter("Setting", ConfiguratorType.Menu)] [JsonPropertyName("setting")]
    public string Setting
    {
        get => _setting;
        set
        {
            SetParameter(ref _setting, value);
            if (SpotifyInfos.Find(SpotifyInfos.Playback, value) is { } info && Kind != info.Kind) TrySetKind(info.Kind);
        }
    }

    public static List<string> GetSettingMenuItems() => SpotifyInfos.Names(SpotifyInfos.Playback);

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public GetSpotifyPlaybackBlock() : base(ValueKind.Text)
    {
        _setting = "Title";
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        var info = SpotifyInfos.Find(SpotifyInfos.Playback, _setting) ?? throw Fail($"unknown setting '{_setting}'");
        var (ok, value) = await spotify.TryGetPlaybackAsync(info, context.CancellationToken);
        if (!ok) throw new BlockFailedException("playback not read");

        // what spotify gave (text, a whole number or true/false), read as the chosen type
        if (!ValueTypes.TryConvert(value!, info.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
