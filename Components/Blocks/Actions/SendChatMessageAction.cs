using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Blocks.Actions;



public class SendChatMessageAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Send Chat Message";
    [JsonIgnore] public override string Description => "Sends a message to your Twitch chat, as the bot if it's logged in ({name} is replaced with a parameter, e.g. {user}).";



    // private



    private string _message;



    // public



    [ConfigurableParameter("Message", ConfiguratorType.Text)] [JsonPropertyName("message")]
    public string Message
    {
        get => _message;
        set => SetParameter(ref _message, value);
    }



    // constructor



    public SendChatMessageAction()
    {
        _message = "";
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager;
        if (chatbot == null)
        {
            AppLog.WriteError("Could not send chat message - no chatbot in global context");
            return;
        }

        var message = context.Format(_message);
        if (string.IsNullOrWhiteSpace(message)) return;

        await chatbot.TrySendChatMessageAsync(message);
    }
}
