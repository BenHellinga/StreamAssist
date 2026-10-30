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
    [JsonIgnore] public override string Description => "Changes a channel point reward's setting on twitch (e.g. its Cost), as the streamer. The reward is its title (or id), {reward} is the one that was just redeemed. Twitch only lets the app that created a reward change it, so create it with New reward in the Events tab. Twitch actions wait their turn, one per second.";



    // private



    private string _setting;
    private readonly ValueSlot<string> _reward;
    private readonly AnyValueSlot _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Setting", ConfiguratorType.Menu)] [JsonPropertyName("setting")]
    public string Setting
    {
        get => _setting;
        set => SetParameter(ref _setting, value);
    }

    public static List<string> GetSettingMenuItems() => RewardSettings.Names.ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



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
        _setting = "Cost";
        _reward = new("Reward");
        _value = new("Value");
        _slots = [_reward, _value];

        // the reward that was just redeemed (a new block starts with this, a saved one loads its own)
        _reward.SetText("{reward}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var setting = RewardSettings.Find(_setting) ?? throw Fail($"unknown setting '{_setting}'");
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
