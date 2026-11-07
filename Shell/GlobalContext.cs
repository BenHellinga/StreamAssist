using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Audio.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Components.Theme.Model;

namespace StreamAssist.Shell;



public static class GlobalContext
{
    public static InputComponentModel? InputComponentModel { get; set; }

    public static DeviceInputManager? DeviceInputManager { get; set; } // which keys are held right now

    public static VoicemeeterManager? VoicemeeterManager {get; set; }

    public static AudioComponentModel? AudioComponentModel { get; set; } // e.g. to connect/disconnect Voicemeeter

    public static WindowLayoutModel? WindowLayoutModel { get; set; }

    public static ChatbotComponentModel? ChatbotComponentModel { get; set; }

    public static ChatbotManager? ChatbotManager { get; set; } // e.g. for sending chat messages

    public static ObsComponentModel? ObsComponentModel { get; set; }

    public static DataModel? DataModel { get; set; } // tables and variables

    public static ObsManager? ObsManager { get; set; } // e.g. for switching scenes

    public static SpotifyComponentModel? SpotifyComponentModel { get; set; }

    public static SpotifyManager? SpotifyManager { get; set; } // e.g. for playing a song

    public static DashboardComponentModel? DashboardComponentModel { get; set; } // e.g. for writing to a console dock

    public static ThemeComponentModel? ThemeComponentModel { get; set; } // e.g. for switching themes
}