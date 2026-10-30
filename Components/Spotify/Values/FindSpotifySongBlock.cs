using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Spotify.Values;



public class FindSpotifySongBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Find Spotify Song";
    [JsonIgnore] public override string Description => "Looks up a song: its Title, Artist, Album, Link or Length Seconds. Song is a link, or words to search for (the first song found), e.g. to reply with what a song request found. Picking a setting picks its type.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _song;
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
            if (SpotifyInfos.Find(SpotifyInfos.Song, value) is { } info && Kind != info.Kind) TrySetKind(info.Kind);
        }
    }

    public static List<string> GetSettingMenuItems() => SpotifyInfos.Names(SpotifyInfos.Song);

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



    // json



    [JsonInclude] [JsonPropertyName("song")]
    private ValueSlotData JsonSong
    {
        get => _song.GetData();
        set => _song.LoadData(value);
    }



    // constructor



    public FindSpotifySongBlock() : base(ValueKind.Text)
    {
        _setting = "Title";
        _song = new("Song");
        _slots = [_song];

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var spotify = GlobalContext.SpotifyManager ?? throw Fail("spotify not set up");
        var info = SpotifyInfos.Find(SpotifyInfos.Song, _setting) ?? throw Fail($"unknown setting '{_setting}'");
        var song = await _song.EvaluateAsync(context);
        if (song.Trim() == "") throw Fail("no song given");

        var (ok, value) = await spotify.TryFindSongAsync(song, info, context.CancellationToken);
        if (!ok) throw new BlockFailedException("song not found");

        // what spotify gave (text, a whole number or true/false), read as the chosen type
        if (!ValueTypes.TryConvert(value!, info.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
