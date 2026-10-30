using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Chatbot.Actions;



public class UnbanUserAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Unban User";
    [JsonIgnore] public override string Description => "Unbans a user (or ends their timeout). User is their login name, {userLogin} is whoever triggered this. The bot (or you) has to be a moderator. Twitch actions wait their turn, one per second.";



    // private



    private readonly ValueSlot<string> _user;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("user")]
    private ValueSlotData JsonUser
    {
        get => _user.GetData();
        set => _user.LoadData(value);
    }



    // constructor



    public UnbanUserAction()
    {
        _user = new("User");
        _slots = [_user];
        _user.SetText("{userLogin}");

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");
        var user = await _user.EvaluateAsync(context);

        // a failure is already logged by the manager
        if (!await chatbot.TryUnbanUserAsync(user, context.CancellationToken)) throw new BlockFailedException("not unbanned");
    }
}
