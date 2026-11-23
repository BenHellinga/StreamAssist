using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class RunAdAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Run Ad";
    [JsonIgnore] public override string Description => "Starts an ad break of this many seconds (30 to 180, affiliates and partners only). Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Stream";



    // private



    private readonly ValueSlot<int> _seconds;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("seconds")]
    private ValueSlotData JsonSeconds
    {
        get => _seconds.GetData();
        set => _seconds.LoadData(value);
    }



    // constructor



    public RunAdAction()
    {
        _seconds = new("Seconds");
        _slots = [_seconds];
        _seconds.SetText("30");

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context) =>
        await Api(context).Twitch.RunAdAsync(await _seconds.EvaluateAsync(context));
}
