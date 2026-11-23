using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class StartPredictionAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Start Prediction";
    [JsonIgnore] public override string Description => "Starts a prediction on your channel: a title, 2 to 10 outcomes (typing in the empty one adds the next) and how many seconds viewers can predict (30 to 1800). End it with End Prediction. Parameters work in the text. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Polls & Predictions";



    // private



    private readonly ValueSlot<string> _title;
    private readonly ValueSlot<int> _seconds;
    private readonly ValueSlotList _outcomes;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Title => _title;
    [JsonIgnore] public ValueSlotList Outcomes => _outcomes;
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("title")]
    private ValueSlotData JsonTitle
    {
        get => _title.GetData();
        set => _title.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("outcomes")]
    private List<ValueSlotData> JsonOutcomes
    {
        get => _outcomes.GetData();
        set => _outcomes.LoadData(value);
    }



    // constructor



    public StartPredictionAction()
    {
        _title = new("Title");
        _seconds = new("Seconds");
        _outcomes = new("Outcomes");
        _slots = [_title, _seconds, _outcomes];

        _seconds.SetText("120");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var title = await _title.EvaluateAsync(context);
        var outcomes = await _outcomes.EvaluateFilledAsync<string>(context);
        var seconds = await _seconds.EvaluateAsync(context);
        await Api(context).Twitch.StartPredictionAsync(title, outcomes, seconds);
    }
}
