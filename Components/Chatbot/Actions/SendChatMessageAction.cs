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
    [JsonIgnore] public override string Description => "Sends a message to your Twitch chat (type it, {name} is replaced with a parameter, e.g. {user}, or place a text block). Sent as the bot if it's connected, otherwise as the streamer, unless Chat only as bot is ticked. Twitch actions wait their turn, one per second.";



    // private



    private readonly ValueSlot<string> _message;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Message => _message;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("message")]
    private ValueSlotData JsonMessage
    {
        get => _message.GetData();
        set => _message.LoadData(value);
    }



    // constructor



    public SendChatMessageAction()
    {
        _message = new("Message");
        _slots = [_message];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager;
        if (chatbot == null) throw Fail("chatbot not set up");

        var message = await _message.EvaluateAsync(context);
        if (string.IsNullOrWhiteSpace(message)) return;

        // waits its turn in the twitch action queue, a failure is already logged there
        if (!await chatbot.TrySendChatMessageAsync(message, context.CancellationToken))
            throw new BlockFailedException("the message wasn't sent");
    }
}
