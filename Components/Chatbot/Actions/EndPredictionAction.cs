using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class EndPredictionAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "End Prediction";
    [JsonIgnore] public override string Description => "Locks the open prediction (no more predicting), resolves it (Winner = an outcome's title, points are paid out) or cancels it (points go back). Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Polls & Predictions";



    // private



    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _winner;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Lock", "Resolve", "Cancel"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("winner")]
    private ValueSlotData JsonWinner
    {
        get => _winner.GetData();
        set => _winner.LoadData(value);
    }



    // constructor



    public EndPredictionAction()
    {
        _change = new("Do", GetChangeMenuItems, "Resolve");
        _winner = new("Winner");
        _slots = [_change, _winner];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        var winner = await _winner.EvaluateAsync(context);
        var twitch = Api(context).Twitch;

        if (change == "Lock") await twitch.LockPredictionAsync();
        else if (change == "Resolve") await twitch.ResolvePredictionAsync(winner);
        else await twitch.CancelPredictionAsync();
    }
}
