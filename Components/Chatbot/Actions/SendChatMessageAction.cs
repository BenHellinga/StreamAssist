using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class SendChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Send Chat Message";
    [JsonIgnore] public override string Description => "Sends a message to your Twitch chat (type it, {name} is replaced with a parameter, e.g. {user}, or place a text block). Sent as the bot if it's connected, otherwise as the streamer, unless Chat only as bot is ticked. Twitch actions wait their turn, one per second. Reply answers the chat message that started this run as a threaded reply (a plain message if it wasn't started by one). Shared Chat: during a shared chat, This channel only keeps it out of the other channels' chats, All channels sends it to every one.";
    [JsonIgnore] public override string? Subgroup => "Chat";



    // private



    private readonly MenuSlot _reply;
    private readonly ValueSlot<string> _message;
    private readonly MenuSlot _sharedChat;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public const string ThisChannelOnly = "This channel only";
    public const string AllChannels = "All channels";

    public static List<string> GetReplyMenuItems() => ["No", "Yes"];
    public static List<string> GetSharedChatMenuItems() => [ThisChannelOnly, AllChannels];

    [JsonIgnore] public ValueSlot<string> Message => _message;
    [JsonIgnore] public MenuSlot Reply => _reply;
    [JsonIgnore] public MenuSlot SharedChat => _sharedChat;
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

    [JsonInclude] [JsonPropertyName("sharedChat")]
    private ValueSlotData JsonSharedChat
    {
        get => _sharedChat.GetData();
        set => _sharedChat.LoadData(value);
    }



    // constructor



    public SendChatMessageAction()
    {
        _reply = new("Reply", GetReplyMenuItems, "Yes");
        _message = new("Message");
        _sharedChat = new("Shared Chat", GetSharedChatMenuItems, ThisChannelOnly);
        _slots = [_reply, _message, _sharedChat];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var reply = await _reply.EvaluateAsync(context);
        var sharedChat = await _sharedChat.EvaluateAsync(context);
        var message = await _message.EvaluateAsync(context);
        await Api(context).Twitch.SendMessageAsync(message, reply == "Yes",
            sharedChat == ThisChannelOnly ? StreamAssist.Scripting.SharedChat.ThisChannelOnly : StreamAssist.Scripting.SharedChat.AllChannels);
    }
}
