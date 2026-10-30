using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class DeleteChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Delete Message";
    [JsonIgnore] public override string Description => "Deletes a chat message by its id, {messageId} is the message that triggered this chat command. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private readonly ValueSlot<string> _messageId;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("messageId")]
    private ValueSlotData JsonMessageId
    {
        get => _messageId.GetData();
        set => _messageId.LoadData(value);
    }



    // constructor



    public DeleteChatMessageAction()
    {
        _messageId = new("Message ID");
        _slots = [_messageId];

        // the message that triggered it (a new block starts with this, a saved one loads its own)
        _messageId.SetText("{messageId}");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");

        var messageId = (await _messageId.EvaluateAsync(context)).Trim();
        if (messageId == "" || messageId == "{messageId}") throw Fail("no message id, only chat has one");

        // waits its turn in the twitch action queue, a failure is already logged there
        if (!await chatbot.TryDeleteChatMessageAsync(messageId, context.CancellationToken))
            throw new BlockFailedException("the message wasn't deleted");
    }
}
