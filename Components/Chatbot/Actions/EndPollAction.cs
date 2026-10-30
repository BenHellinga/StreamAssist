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



    // private



    private string _results;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Results", ConfiguratorType.Menu)] [JsonPropertyName("results")]
    public string Results
    {
        get => _results;
        set => SetParameter(ref _results, value);
    }

    public static List<string> GetResultsMenuItems() => ["Show", "Hide"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public EndPollAction()
    {
        _results = "Show";
        _slots = [];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        // a failure (e.g. no poll is running) is already logged by the manager
        if (!await chatbot.TryEndPollAsync(_results == "Show", context.CancellationToken)) throw new BlockFailedException("poll not ended");
    }
}
