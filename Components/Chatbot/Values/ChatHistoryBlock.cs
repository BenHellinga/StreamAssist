using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Values;



public class ChatHistoryBlock : ValueBlock<string>
{
    // info



    [JsonIgnore] public override string Name => "Chat History";
    [JsonIgnore] public override string Description => "Something about a recent chat message (the app keeps the last 100 while it runs). Find picks it: the Newest, or by Message Id that one (This), the one just Older or just Newer, staying put at either end, an empty id counting as the newest. Give picks what you get, e.g. its messageId to remember where you are, its message, or its userLogin to time them out.";



    // private



    private readonly MenuSlot _find;
    private readonly MenuSlot _give;
    private readonly ValueSlot<string> _messageId;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetFindMenuItems() => ["Newest", "This", "Older", "Newer"];


    public static List<string> GetGiveMenuItems() => ChatEvents.Chat.Select(p => p.Name).ToList();

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("find")]
    private ValueSlotData JsonFind
    {
        get => _find.GetData();
        set => _find.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("give")]
    private ValueSlotData JsonGive
    {
        get => _give.GetData();
        set => _give.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("messageId")]
    private ValueSlotData JsonMessageId
    {
        get => _messageId.GetData();
        set => _messageId.LoadData(value);
    }



    // constructor



    public ChatHistoryBlock()
    {
        _find = new("Find", GetFindMenuItems, "Newest");
        _give = new("Give", GetGiveMenuItems, "message");
        _messageId = new("Message Id");
        _slots = [_find, _give, _messageId];

        TrackSlots();
    }



    // compute



    protected override async Task<string> ComputeAsync(TriggerContext context)
    {
        var find = await _find.EvaluateAsync(context);
        var give = await _give.EvaluateAsync(context);
        var history = GlobalContext.ChatbotComponentModel?.ChatHistory ?? throw Fail("no chatbot");

        var step = find switch
        {
            "This" => ChatHistoryStep.This,
            "Older" => ChatHistoryStep.Older,
            "Newer" => ChatHistoryStep.Newer,
            _ => ChatHistoryStep.Newest,
        };

        // the id is only read when it's used (Newest needs none)
        var id = step == ChatHistoryStep.Newest ? "" : await _messageId.EvaluateAsync(context);
        if (!history.TryFind(step, id, out var message, out var error)) throw Fail(error);

        return message.TryGetValue(give, out var value) ? value : "";
    }
}
