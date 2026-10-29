using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class TimeOutUserAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Time Out User";
    [JsonIgnore] public override string Description => "Times a user out of your chat for some seconds (1 to 1209600, two weeks). User is their login name, {userLogin} is whoever triggered this. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private const int MaxSeconds = 1_209_600; // twitch's limit, two weeks

    private readonly ValueSlot<string> _user;
    private readonly ValueSlot<int> _seconds;
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

    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("reason")]
    private ValueSlotData JsonReason
    {
        get => _reason.GetData();
        set => _reason.LoadData(value);
    }



    // constructor



    public TimeOutUserAction()
    {
        _user = new("User");
        _seconds = new("Seconds");
        _reason = new("Reason");
        _slots = [_user, _seconds, _reason];

        // whoever triggered it, for a minute (a new block starts with these, a saved one loads its own)
        _user.SetText("{userLogin}");
        _seconds.SetText("60");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("no chatbot in global context");

        var user = await _user.EvaluateAsync(context);
        var seconds = await _seconds.EvaluateAsync(context);
        var reason = await _reason.EvaluateAsync(context);

        if (seconds < 1 || seconds > MaxSeconds) throw Fail($"{seconds} seconds is outside what twitch allows (1 to {MaxSeconds})");

        // waits its turn in the twitch action queue, a failure is already logged there
        if (!await chatbot.TryTimeOutUserAsync(user, seconds, reason, context.CancellationToken))
            throw new BlockFailedException($"'{user}' wasn't timed out");
    }
}
