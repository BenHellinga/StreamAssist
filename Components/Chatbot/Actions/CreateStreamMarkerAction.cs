using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class CreateStreamMarkerAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Create Stream Marker";
    [JsonIgnore] public override string Description => "Marks this moment of the stream (only while live), with an optional description, so it's easy to find in the VOD. Done as the streamer. Twitch actions wait their turn, one per second.";



    // private



    private readonly ValueSlot<string> _description;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("description")]
    private ValueSlotData JsonDescription
    {
        get => _description.GetData();
        set => _description.LoadData(value);
    }



    // constructor



    public CreateStreamMarkerAction()
    {
        _description = new("Description");
        _slots = [_description];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var description = await _description.EvaluateAsync(context);

        if (!await chatbot.TryCreateMarkerAsync(description, context.CancellationToken)) throw new BlockFailedException("no marker");
    }
}
