using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Shell;



public static class GlobalContext
{
    public static KeyboardComponentModel? KeyboardComponentModel { get; set; }

    public static KeyboardInputManager? KeyboardInputManager { get; set; } // which keys are held right now

    public static VoicemeeterManager? VoicemeeterManager {get; set; }

    public static WindowLayoutModel? WindowLayoutModel { get; set; }

    public static ChatbotComponentModel? ChatbotComponentModel { get; set; }

    public static ChatbotManager? ChatbotManager { get; set; } // e.g. for sending chat messages
}