using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class SetRewardSettingAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Reward Setting";
    [JsonIgnore] public override string Description => "Changes a channel point reward's setting on Twitch (e.g. its Cost). The reward is its title (or id), {reward} is the one that was just redeemed. Twitch only lets the app that created a reward change it, so create it with New redeem in the Events tab. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Rewards";



    // private



    private readonly MenuSlot _setting;
    private readonly ValueSlot<string> _reward;
    private readonly AnyValueSlot _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetSettingMenuItems() => RewardSettings.Names.ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



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

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetRewardSettingAction()
    {
        _setting = new("Setting", GetSettingMenuItems, "Cost");
        _reward = new("Reward");
        _value = new("Value");
        _slots = [_setting, _reward, _value];

        // the reward that was just redeemed (a new block starts with this, a saved one loads its own)
        _reward.SetText("{reward}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var settingOption = await _setting.EvaluateAsync(context);
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var setting = RewardSettings.Find(settingOption) ?? throw Fail($"unknown setting '{settingOption}'");
        var reward = await _reward.EvaluateAsync(context);

        // typed text or any block, turned into what the setting needs (e.g. a whole number for Cost)
        var (kind, raw) = await _value.EvaluateRawAsync(context);
        if (!ValueTypes.TryConvert(raw, kind ?? ValueKind.Text, setting.Kind, out var value, out var error))
            throw Fail($"Value: {error}");

        // waits its turn in the twitch action queue, a failure is already logged there
        if (!await chatbot.TrySetRewardSettingAsync(reward, setting, value, context.CancellationToken))
            throw new BlockFailedException($"'{setting.Name}' of reward '{reward}' wasn't changed");
    }
}
