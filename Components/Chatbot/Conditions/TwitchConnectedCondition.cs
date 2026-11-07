using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Conditions;



// whether a Twitch account is connected (the Connections tab): the streamer's (everything but chat goes through it) or
// the bot's
public class TwitchConnectedCondition : ConditionBlock
{
    // info



    [JsonIgnore] public override string Name => "Twitch Connected";
    [JsonIgnore] public override string Description => "True while that Twitch account is connected (the Connections tab): Streamer, or Bot, e.g. an If around Twitch blocks so they're skipped while it isn't, instead of failing.";



    // private



    private readonly MenuSlot _account;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetAccountMenuItems() => ["Streamer", "Bot"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("account")]
    private ValueSlotData JsonAccount
    {
        get => _account.GetData();
        set => _account.LoadData(value);
    }



    // constructor



    public TwitchConnectedCondition()
    {
        _account = new("Account", GetAccountMenuItems, "Streamer");
        _slots = [_account];

        TrackSlots();
    }



    // check



    protected override async Task<bool> CheckAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager;
        var status = await _account.EvaluateAsync(context) == "Bot" ? chatbot?.BotConnection : chatbot?.StreamerConnection;
        return status == ConnectionStatus.Connected;
    }
}
