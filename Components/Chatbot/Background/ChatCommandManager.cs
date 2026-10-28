using System;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Background;



// runs every chat message through the chat block tree (starting at the root slot), the first path that reaches a bindable runs it
public class ChatCommandManager : IDisposable
{
    // private



    private readonly ChatbotComponentModel _model;
    private readonly ChatbotManager _manager;



    // constructor



    public ChatCommandManager(ChatbotComponentModel model, ChatbotManager manager)
    {
        _model = model;
        _manager = manager;

        _manager.EventReceived += OnEventReceived;
    }



    // background events



    private void OnEventReceived(TwitchEvent e)
    {
        if (e.Type != TwitchEvent.ChatMessage) return;

        // the bot's own messages never trigger commands (so it can't set itself off)
        if (_manager.BotUserId != null && e.GetString("chatter_user_id") == _manager.BotUserId) return;

        var text = e.GetString("message", "text") ?? "";
        ChatBlock.TryMatchSlot(_model.Root, new ChatMatchState(text, 0, e.ChatParameters()));
    }



    // disposal



    public void Dispose() => _manager.EventReceived -= OnEventReceived;
}
