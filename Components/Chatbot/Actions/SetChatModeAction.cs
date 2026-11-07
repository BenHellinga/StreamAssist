using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class SetChatModeAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Chat Mode";
    [JsonIgnore] public override string Description => "Turns a chat mode on or off, or toggles it: slow mode (Duration = seconds between messages, 3 to 120), followers only (Duration = minutes they must have followed), subscribers only, emote only, unique chat. Duration can stay empty. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private readonly MenuSlot _mode;
    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _duration;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetModeMenuItems() => TwitchInfos.ChatModes.ToList();


    public static List<string> GetChangeMenuItems() => ["On", "Off", "Toggle"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("mode")]
    private ValueSlotData JsonMode
    {
        get => _mode.GetData();
        set => _mode.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("duration")]
    private ValueSlotData JsonDuration
    {
        get => _duration.GetData();
        set => _duration.LoadData(value);
    }



    // constructor



    public SetChatModeAction()
    {
        _mode = new("Mode", GetModeMenuItems, "Slow Mode");
        _change = new("Do", GetChangeMenuItems, "Toggle");
        _duration = new("Duration");
        _slots = [_mode, _change, _duration];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var mode = await _mode.EvaluateAsync(context);
        var change = await _change.EvaluateAsync(context);
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        // empty duration = leave it as twitch has it
        var durationText = (await _duration.EvaluateAsync(context)).Trim();
        int? duration = null;
        if (durationText != "")
        {
            if (!int.TryParse(durationText, out var parsed)) throw Fail($"Duration '{durationText}' isn't a whole number");
            duration = parsed;
        }

        bool? on = change == "On" ? true : change == "Off" ? false : null;
        if (!await chatbot.TrySetChatModeAsync(mode, on, duration, context.CancellationToken)) throw new BlockFailedException("chat mode not set");
    }
}
