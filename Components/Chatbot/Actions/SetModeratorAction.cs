using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Actions;



public class SetModeratorAction : ActionBlock
{
    // info



    [JsonIgnore] public override string Name => "Set Moderator";
    [JsonIgnore] public override string Description => "Makes a user a moderator of your channel, or removes it. User is their login name, {userLogin} is whoever triggered this. Done as the streamer. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Moderation";



    // private



    private readonly MenuSlot _change;
    private readonly ValueSlot<string> _user;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetChangeMenuItems() => ["Add", "Remove"];

    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("change")]
    private ValueSlotData JsonChange
    {
        get => _change.GetData();
        set => _change.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("user")]
    private ValueSlotData JsonUser
    {
        get => _user.GetData();
        set => _user.LoadData(value);
    }



    // constructor



    public SetModeratorAction()
    {
        _change = new("Do", GetChangeMenuItems, "Add");
        _user = new("User");
        _slots = [_change, _user];
        _user.SetText("{userLogin}");

        TrackSlots();
    }



    // execute



    // twitch requests log their own failure, then the run stops
    protected override async Task ExecuteAsync(TriggerContext context)
    {
        var change = await _change.EvaluateAsync(context);
        var user = await _user.EvaluateAsync(context);
        await Api(context).Twitch.SetModeratorAsync(user, change == "Add");
    }
}
