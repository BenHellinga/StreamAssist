using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



// shared by Add / Edit / Delete Command: when a chat message ran it, the chatter gets a short reply instead of a console
// error (the model logs only when it wasn't chat), then the run stops either way so nothing after it (e.g. "Added!") runs
public static class CommandActionHelper
{
    public static async Task<BlockFailedException> FailAsync(TriggerContext context, CommandFailure failure, string reason)
    {
        var reply = failure switch
        {
            CommandFailure.NotFound => "command not found",
            CommandFailure.AlreadyExists => "command already exists",
            CommandFailure.NotSimple => "could not edit command",
            _ => null, // a typo in the command itself (no name, no response) gets no reply
        };

        // the reply waits its turn in the twitch action queue, a failure to send is logged there
        if (reply != null && ChatEvents.IsFromChat(context.Parameters) && GlobalContext.ChatbotManager is { } chatbot)
            await chatbot.TrySendChatMessageAsync(reply, context.CancellationToken);

        return new BlockFailedException(reason);
    }
}
