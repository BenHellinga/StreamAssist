using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class StartPollAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Start Poll";
    [JsonIgnore] public override string Description => "Starts a poll on your channel: a title, 2 to 5 choices (typing in the empty one adds the next) and how many seconds it runs (15 to 1800). Parameters work in the text. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Polls & Predictions";



    // private



    private readonly ValueSlot<string> _title;
    private readonly ValueSlot<int> _seconds;
    private readonly ValueSlotList _choices;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public ValueSlot<string> Title => _title;
    [JsonIgnore] public ValueSlotList Choices => _choices;
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

    [JsonInclude] [JsonPropertyName("choices")]
    private List<ValueSlotData> JsonChoices
    {
        get => _choices.GetData();
        set => _choices.LoadData(value);
    }



    // constructor



    public StartPollAction()
    {
        _title = new("Title");
        _seconds = new("Seconds");
        _choices = new("Choices");
        _slots = [_title, _seconds, _choices];

        _seconds.SetText("60");

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var title = await _title.EvaluateAsync(context);
        var choices = await _choices.EvaluateFilledAsync<string>(context);
        var seconds = await _seconds.EvaluateAsync(context);
        await Api(context).Twitch.StartPollAsync(title, choices, seconds);
    }
}
