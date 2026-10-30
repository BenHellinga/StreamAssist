using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class CreateClipAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Create Clip";
    [JsonIgnore] public override string Description => "Clips the last ~30 seconds (only while live) and puts the clip's link in a parameter for the blocks after it, e.g. {clipUrl} in a Send Chat Message. Done as the streamer. Twitch actions wait their turn, one per second.";



    // private



    private string _parameterName;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [ConfigurableParameter("Save Link As", ConfiguratorType.Text)] [JsonPropertyName("parameterName")]
    public string ParameterName
    {
        get => _parameterName;
        set => SetParameter(ref _parameterName, value);
    }

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public CreateClipAction()
    {
        _parameterName = "clipUrl";
        _slots = [];

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var name = _parameterName.Trim();
        if (!TriggerContext.IsValidName(name)) throw Fail($"'{name}' isn't a valid name, use letters, digits, _");

        var (ok, url) = await chatbot.TryCreateClipAsync(context.CancellationToken);
        if (!ok) throw new BlockFailedException("no clip");

        context.SetParameter(name, url!);
    }
}
