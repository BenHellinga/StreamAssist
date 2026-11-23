using System;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.CSharp.Api;
using StreamAssist.Scripting;

namespace StreamAssist.Components.Chatbot.Actions;



// shared by Add / Edit / Delete Command: when a chat message ran it, the chatter gets a short reply instead of a console
// error, anywhere else the block logs why; the run stops either way, so nothing after it (e.g. "Added!") runs
public static class CommandActionHelper
{
    public static async Task RunAsync(TriggerContext context, Action<ITwitch> change)
    {
        var twitch = new ScriptApi(context).Twitch;
        try
        {
            change(twitch);
        }
        catch (ChatCommandException ex) when (ChatEvents.IsFromChat(context.Parameters))
        {
            // the reply waits its turn in the twitch action queue, a failure to send is logged there
            if (ex.ChatReply != null)
            {
                try
                {
                    await twitch.SendMessageAsync(ex.ChatReply);
                }
                catch (StreamAssistException)
                {
                    // already logged
                }
            }

            throw new BlockFailedException(ex.Message);
        }
    }
}
