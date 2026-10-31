using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Values;



public class GetRewardSettingBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Reward Setting";
    [JsonIgnore] public override string Description => "Reads a channel point reward's setting from Twitch right now (e.g. its Cost). The reward is its title (or id), {reward} is the one that was just redeemed. Works on any reward. Done as the streamer. Twitch actions wait their turn, one per second.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _reward;
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
            if (RewardSettings.Find(value) is { } setting && Kind != setting.Kind) TrySetKind(setting.Kind);
        }
    }

    public static List<string> GetSettingMenuItems() => RewardSettings.Names.ToList();

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



    [JsonInclude] [JsonPropertyName("reward")]
    private ValueSlotData JsonReward
    {
        get => _reward.GetData();
        set => _reward.LoadData(value);
    }



    // constructor



    public GetRewardSettingBlock() : base(ValueKind.Int)
    {
        _setting = "Cost";
        _reward = new("Reward");
        _slots = [_reward];

        // the reward that was just redeemed (a new block starts with this, a saved one loads its own)
        _reward.SetText("{reward}");

        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var setting = RewardSettings.Find(_setting) ?? throw Fail($"unknown setting '{_setting}'");
        var reward = await _reward.EvaluateAsync(context);

        // waits its turn in the twitch action queue, a failure is already logged there
        var (ok, value) = await chatbot.TryGetRewardSettingAsync(reward, setting, context.CancellationToken);
        if (!ok) throw new BlockFailedException($"'{setting.Name}' of reward '{reward}' wasn't read");

        if (!ValueTypes.TryConvert(value!, setting.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
