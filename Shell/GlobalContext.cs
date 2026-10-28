using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Shell;



public static class GlobalContext
{
    public static KeyboardComponentModel? KeyboardComponentModel { get; set; }

    public static VoicemeeterManager? VoicemeeterManager {get; set; }
}