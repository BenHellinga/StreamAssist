using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.Actions;



public class SendChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Send Chat Message";
    [JsonIgnore] public override string Description => "Sends a message to your Twitch chat, as the bot if it's logged in (type it, {name} is replaced with a parameter, e.g. {user}, or place a text block).";



    // private



    private readonly ValueSlot<string> _message;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



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
        if (chatbot == null) throw Fail("no chatbot in global context");

        var message = await _message.EvaluateAsync(context);
        if (string.IsNullOrWhiteSpace(message)) return;

        await chatbot.TrySendChatMessageAsync(message);
    }
}
