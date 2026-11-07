using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class EndPollAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "End Poll";
    [JsonIgnore] public override string Description => "Ends the poll that's running, showing its results or hiding them. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Polls & Predictions";



    // private



    private readonly MenuSlot _results;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetResultsMenuItems() => ["Show", "Hide"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("results")]
    private ValueSlotData JsonResults
    {
        get => _results.GetData();
        set => _results.LoadData(value);
    }



    // constructor



    public EndPollAction()
    {
        _results = new("Results", GetResultsMenuItems, "Show");
        _slots = [_results];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var results = await _results.EvaluateAsync(context);
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        // a failure (e.g. no poll is running) is already logged by the manager
        if (!await chatbot.TryEndPollAsync(results == "Show", context.CancellationToken)) throw new BlockFailedException("poll not ended");
    }
}
