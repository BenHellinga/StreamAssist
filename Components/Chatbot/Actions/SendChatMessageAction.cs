using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class SendChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Send Chat Message";
    [JsonIgnore] public override string Description => "Sends a message to your Twitch chat (type it, {name} is replaced with a parameter, e.g. {user}, or place a text block). Sent as the bot if it's connected, otherwise as the streamer, unless Chat only as bot is ticked. Twitch actions wait their turn, one per second. Reply answers the chat message that started this run as a threaded reply (a plain message if it wasn't started by one).";



    // private



    private readonly MenuSlot _reply;
    private readonly ValueSlot<string> _message;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetReplyMenuItems() => ["No", "Yes"];

    [JsonIgnore] public ValueSlot<string> Message => _message;
    [JsonIgnore] public MenuSlot Reply => _reply;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("reply")]
    private ValueSlotData JsonReply
    {
        get => _reply.GetData();
        set => _reply.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("message")]
    private ValueSlotData JsonMessage
    {
        get => _message.GetData();
        set => _message.LoadData(value);
    }



    // constructor



    public SendChatMessageAction()
    {
        _reply = new("Reply", GetReplyMenuItems, "No");
        _message = new("Message");
        _slots = [_reply, _message];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var reply = await _reply.EvaluateAsync(context);
        var chatbot = GlobalContext.ChatbotManager;
        if (chatbot == null) throw Fail("chatbot not set up");

        var message = await _message.EvaluateAsync(context);
        if (string.IsNullOrWhiteSpace(message)) return;

        // waits its turn in the twitch action queue, a failure is already logged there
        var replyTo = reply == "Yes" && context.Parameters.TryGetValue("messageId", out var id) ? id : null;
        if (!await chatbot.TrySendChatMessageAsync(message, context.CancellationToken, replyTo))
            throw new BlockFailedException("the message wasn't sent");
    }
}
