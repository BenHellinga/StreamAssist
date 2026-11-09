using StreamAssist.Components.OBS.Model;
using StreamAssist.Components.Spotify.Actions;
using StreamAssist.Components.Spotify.Background;
using StreamAssist.Components.Spotify.Model;
using StreamAssist.Components.Spotify.Values;
using StreamAssist.Components.Data.Model;
using StreamAssist.Components.Data.Background;
using StreamAssist.Components.Data.Actions;
using StreamAssist.Components.Data.Containers;
using StreamAssist.Components.Data.Values;
using System.Net;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Conditions;
using StreamAssist.Components.OBS.Values;
using StreamAssist.Components.OBS.Actions;
using StreamAssist.Components.OutputConsole.Actions;
using StreamAssist.Components.Input.Conditions;
using StreamAssist.Components.Audio.Actions;
using StreamAssist.Core.Connections;
using StreamAssist.Components.Chatbot.Values;
using StreamAssist.Components.Chatbot.Actions;
using StreamAssist.Components.Blocks.Values;
using StreamAssist.Components.Blocks.Actions;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Chatbot.ChatBlocks;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Components.Blocks.Conditions;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Input.UI;
using System.Text.Json;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.Containers;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Tests;



// every engine check, run in order, returns the names of the checks that failed. Each section is a method in the
// EngineChecks.<area>.cs files. The first sections count console errors, which AppLog posts through Avalonia's
// dispatcher, so they run before anything awaits, while this thread still owns it: keep new error-counting sections
// above the first awaited one
internal static partial class EngineChecks
{



    private static Action<string> _log = _ => { };



    private static List<string> _failed = [];



    public static async Task<List<string>> RunAsync(Action<string> log)
    {
        _log = log;
        _failed = [];

        ValueErrors();
        EditableCommands();
        EditableCommandsUnderPrefix();
        PlainTextAndRegexRows();
        MathAndRewardSettings();
        ConnectionRows();
        Expression();
        Dashboard();
        SetParameter();
        Obs();
        Spotify();
        VoicemeeterAndTwitchInfo();
        ParseParameterSpacing();
        await BindLabels(); // the first await: every section above counts console errors
        await Bindables();
        await IfBranches();
        await EmptyBindables();
        KeyNameParsing();
        await LogicConditions();
        await KeyStateAndKeybinds();
        RawKeysAndConnections();
        EventSubParsing();
        await ChatHistory();
        ChatbotModelAndMatchRows();
        await ChatMatching();
        await RedeemsAndStreamEvents();
        ConnectionSettings();
        await ValueBlocksAndSlots();
        await AnyTypeBlocks();
        await ActionQueueAndModeration();
        DuplicateChatMessages();
        await LoopsWaitStop();
        ConfigFiles();
        await DataTablesAndVariables();
        AssetPaths();
        await GetTime();
        ThemeChanges();
        KokoroVoiceNames();
        await CopyPaste();
        await ChangeConfig();
        SoundQueue();
        MenuSlots();
        ListTypeFilter();
        WidgetModels();
        PlinkoModels();
        await WidgetBlockChecks();
        await WidgetServerChecks();
        await Scripts();

        return _failed;
    }



    // shared helpers



    private static void Log(string text) => _log(text);



    private static void Check(string name, bool ok)
    {
        Log($"{(ok ? "PASS" : "FAIL")}  {name}");
        if (!ok) _failed.Add(name);
    }



    private static ChatbotComponentModel Chatbot() { var c = new ChatbotComponentModel(); c.ChatConfigs.TryCreate("Default"); c.EventsConfigs.TryCreate("Default"); return c; }



    private static InputListModel Keyboards() { var k = new InputListModel(); k.Configs.TryCreate("Default"); return k; }



    private static ConvertBlock IntParam(string name) { var c = new ConvertBlock { To = "Int" }; ((ValueSlot)c.Slots[0]).SetText($"{{{name}}}"); return c; }



    private static ConvertBlock DoubleParam(string name) { var c = new ConvertBlock { To = "Double" }; ((ValueSlot)c.Slots[0]).SetText($"{{{name}}}"); return c; }



    private static ConvertBlock MakeConvert(string to, string text) { var c = new ConvertBlock { To = to }; ((ValueSlot)c.Slots[0]).SetText(text); return c; }
}
