using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Values;



public class GetFollowerInfoBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Follower Info";
    [JsonIgnore] public override string Description => "Whether a user follows you, how many days, or since when (e.g. for !followage). User is their login name, {userLogin} is whoever triggered this. Picking one picks its type. Twitch actions wait their turn, one per second.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _user;
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
            if (TwitchInfos.Find(TwitchInfos.Follower, value) is { } info && Kind != info.Kind) TrySetKind(info.Kind);
        }
    }

    public static List<string> GetSettingMenuItems() => TwitchInfos.Names(TwitchInfos.Follower);

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



    [JsonInclude] [JsonPropertyName("user")]
    private ValueSlotData JsonUser
    {
        get => _user.GetData();
        set => _user.LoadData(value);
    }



    // constructor



    public GetFollowerInfoBlock() : base(ValueKind.Int)
    {
        _setting = "Days Following";
        _user = new("User");
        _slots = [_user];
        _user.SetText("{userLogin}");

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var info = TwitchInfos.Find(TwitchInfos.Follower, _setting) ?? throw Fail($"unknown setting '{_setting}'");
        var user = await _user.EvaluateAsync(context);

        var (ok, value) = await chatbot.TryGetFollowerInfoAsync(user, info, context.CancellationToken);
        if (!ok) throw new BlockFailedException("follower info not read");

        // what twitch gave (text, a whole number or true/false), read as the chosen type
        if (!ValueTypes.TryConvert(value!, info.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
