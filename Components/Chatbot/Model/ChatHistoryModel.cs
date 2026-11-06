using System;
using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.Chatbot.Model;



// the last chat messages (the newest MaxMessages), only while the app runs: twitch has no way to read past chat, so
// the app keeps what it heard. Each is the message's chat parameters (user, message, messageId, ...). Blocks step
// through it by message id, not by position, so a message arriving meanwhile doesn't move what's being looked at
public sealed class ChatHistoryModel
{
    // constants



    public const int MaxMessages = 100;



    // private



    private readonly List<IReadOnlyDictionary<string, string>> _messages; // oldest first



    // public



    public IReadOnlyList<IReadOnlyDictionary<string, string>> Messages => _messages;



    // events



    public event Action? Changed;



    // constructor



    public ChatHistoryModel()
    {
        _messages = [];
    }



    // public api



    // the ChatCommandManager adds every message it hears (its own copy, so later changes to a run's parameters don't reach it)
    public void Add(IReadOnlyDictionary<string, string> parameters)
    {
        _messages.Add(new Dictionary<string, string>(parameters));
        if (_messages.Count > MaxMessages) _messages.RemoveAt(0);

        Changed?.Invoke();
    }



    // the newest message, or by id that one / the one just older / just newer. Stepping past either end stays on the
    // end one; an empty id counts as the newest (so the first step back from nothing shows the newest). The error is
    // for the block to log
    public bool TryFind(ChatHistoryStep step, string messageId, out IReadOnlyDictionary<string, string> message, out string error)
    {
        message = null!;
        error = "";

        if (_messages.Count == 0)
        {
            error = "no chat messages yet";
            return false;
        }

        var id = messageId.Trim();
        if (step == ChatHistoryStep.Newest || id == "")
        {
            message = _messages[^1];
            return true;
        }

        var index = _messages.FindIndex(m => m.TryGetValue("messageId", out var mid) && mid == id);
        if (index < 0)
        {
            error = $"message '{id}' isn't in the chat history (only the last {MaxMessages} are kept)";
            return false;
        }

        index = step switch
        {
            ChatHistoryStep.Older => Math.Max(index - 1, 0),
            ChatHistoryStep.Newer => Math.Min(index + 1, _messages.Count - 1),
            _ => index,
        };

        message = _messages[index];
        return true;
    }



    public void Clear()
    {
        if (_messages.Count == 0) return;

        _messages.Clear();
        Changed?.Invoke();
    }
}



public enum ChatHistoryStep { Newest, This, Older, Newer }
