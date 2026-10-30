using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class EndPredictionAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "End Prediction";
    [JsonIgnore] public override string Description => "Locks the open prediction (no more predicting), resolves it (Winner = an outcome's title, points are paid out) or cancels it (points go back). Done as the streamer. Twitch actions wait their turn, one per second.";



    // private



    private string _change;
    private readonly ValueSlot<string> _winner;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Do", ConfiguratorType.Menu)] [JsonPropertyName("change")]
    public string Change
    {
        get => _change;
        set => SetParameter(ref _change, value);
    }

    public static List<string> GetChangeMenuItems() => ["Lock", "Resolve", "Cancel"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("winner")]
    private ValueSlotData JsonWinner
    {
        get => _winner.GetData();
        set => _winner.LoadData(value);
    }



    // constructor



    public EndPredictionAction()
    {
        _change = "Resolve";
        _winner = new("Winner");
        _slots = [_winner];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var winner = await _winner.EvaluateAsync(context);
        if (_change == "Resolve" && winner.Trim() == "") throw Fail("Resolve needs a Winner");

        if (!await chatbot.TryEndPredictionAsync(_change, winner, context.CancellationToken)) throw new BlockFailedException("prediction not ended");
    }
}
