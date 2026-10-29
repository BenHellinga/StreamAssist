using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class BanUserAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Ban User";
    [JsonIgnore] public override string Description => "Bans a user from your chat. User is their login name, {userLogin} is whoever triggered this. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private readonly ValueSlot<string> _user;
    private readonly ValueSlot<string> _reason;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("user")]
    private ValueSlotData JsonUser
    {
        get => _user.GetData();
        set => _user.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("reason")]
    private ValueSlotData JsonReason
    {
        get => _reason.GetData();
        set => _reason.LoadData(value);
    }



    // constructor



    public BanUserAction()
    {
        _user = new("User");
        _reason = new("Reason");
        _slots = [_user, _reason];

        // whoever triggered it (a new block starts with this, a saved one loads its own)
        _user.SetText("{userLogin}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("no chatbot in global context");

        var user = await _user.EvaluateAsync(context);
        var reason = await _reason.EvaluateAsync(context);

        // waits its turn in the twitch action queue, a failure is already logged there
        if (!await chatbot.TryBanUserAsync(user, reason, context.CancellationToken))
            throw new BlockFailedException($"'{user}' wasn't banned");
    }
}
