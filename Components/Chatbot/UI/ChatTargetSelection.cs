using System;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.UI;



// which bind box is open in the bottom panel editor (at most one, like a selected key)
// shared because the bind boxes are created deep inside the block tree, where the chatbot tab can't hand them anything
public static class ChatTargetSelection
{
    // private



    private static ChatTarget? _selected;



    // public



    public static ChatTarget? Selected => _selected;



    // events



    public static event Action? Changed;



    // public api



    // clicking the open bind box again closes it
    public static void Toggle(ChatTarget target) => Set(_selected == target ? null : target);



    public static void Set(ChatTarget? target)
    {
        if (_selected == target) return;

        _selected = target;
        Changed?.Invoke();
    }
}
