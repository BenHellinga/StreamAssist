using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class DeleteChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Delete Chat Message";
    [JsonIgnore] public override string Description => "Deletes a chat message by its id, {messageId} is the message that triggered this chat command. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Chat";



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



    protected override async Task ExecuteAsync(TriggerContext context) =>
        await Api(context).Twitch.DeleteMessageAsync(await _messageId.EvaluateAsync(context));
}
