using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class SetStreamInfoAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Stream Info";
    [JsonIgnore] public override string Description => "Changes your stream title, or category (by name, e.g. Just Chatting; the closest match if it isn't exact). Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Stream";



    // private



    private readonly MenuSlot _setting;
    private readonly ValueSlot<string> _value;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetSettingMenuItems() => ["Title", "Category"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("value")]
    private ValueSlotData JsonValue
    {
        get => _value.GetData();
        set => _value.LoadData(value);
    }



    // constructor



    public SetStreamInfoAction()
    {
        _setting = new("Setting", GetSettingMenuItems, "Title");
        _value = new("Value");
        _slots = [_setting, _value];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var setting = await _setting.EvaluateAsync(context);
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var value = (await _value.EvaluateAsync(context)).Trim();
        if (value == "") throw Fail("Value is empty");

        if (!await chatbot.TrySetStreamInfoAsync(setting, value, context.CancellationToken)) throw new BlockFailedException("stream info not set");
    }
}
