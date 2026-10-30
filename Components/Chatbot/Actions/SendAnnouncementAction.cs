using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class SendAnnouncementAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Send Announcement";
    [JsonIgnore] public override string Description => "Sends a highlighted announcement to chat in a colour (parameters like {user} work in the message). The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private string _color;
    private readonly ValueSlot<string> _message;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Color", ConfiguratorType.Menu)] [JsonPropertyName("color")]
    public string Color
    {
        get => _color;
        set => SetParameter(ref _color, value);
    }

    public static List<string> GetColorMenuItems() => TwitchInfos.AnnouncementColors.ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("message")]
    private ValueSlotData JsonMessage
    {
        get => _message.GetData();
        set => _message.LoadData(value);
    }



    // constructor



    public SendAnnouncementAction()
    {
        _color = "Primary";
        _message = new("Message");
        _slots = [_message];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var message = await _message.EvaluateAsync(context);
        if (string.IsNullOrWhiteSpace(message)) return;

        if (!await chatbot.TrySendAnnouncementAsync(message, _color, context.CancellationToken)) throw new BlockFailedException("announcement not sent");
    }
}
