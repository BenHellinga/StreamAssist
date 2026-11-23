using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Values;



public class GetRewardSettingBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Reward Setting";
    [JsonIgnore] public override string Description => "Reads a channel point reward's setting from Twitch right now (e.g. its Cost). The reward is its title (or id), {reward} is the one that was just redeemed. Works on any reward. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Rewards";



    // private



    private readonly MenuSlot _setting;
    private readonly ValueSlot<string> _reward;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // picking a setting also picks its type below (when the slot it's in can take it)
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



    // menu events



    // picking an option in the dropdown (not a block in it: that's only known when it runs)
    private void OnSettingPicked()
    {
        var value = _setting.Text;
        if (RewardSettings.Find(value) is { } setting && Kind != setting.Kind) TrySetKind(setting.Kind);
    }



    // json



    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("reward")]
    private ValueSlotData JsonReward
    {
        get => _reward.GetData();
        set => _reward.LoadData(value);
    }



    // constructor



    public GetRewardSettingBlock() : base(ValueKind.Int)
    {
        _setting = new("Setting", GetSettingMenuItems, "Cost");
        _reward = new("Reward");
        _slots = [_setting, _reward];

        // the reward that was just redeemed (a new block starts with this, a saved one loads its own)
        _reward.SetText("{reward}");

        _setting.TextChanged += OnSettingPicked;
        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var settingOption = await _setting.EvaluateAsync(context);
        var setting = RewardSettings.Find(settingOption) ?? throw Fail($"unknown setting '{settingOption}'");
        var reward = await Api(context).Twitch.GetRewardAsync(await _reward.EvaluateAsync(context));
        if (!ValueTypes.TryConvert(setting.Get(reward), setting.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
