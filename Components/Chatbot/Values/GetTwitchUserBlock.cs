using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Values;



public class GetTwitchUserBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Twitch User";
    [JsonIgnore] public override string Description => "Reads a Twitch account: Display Name, Id, Account Age Days, Created On or Description (e.g. for !accountage). User is their login name. Picking a setting picks its type. Twitch actions wait their turn, one per second.";
    [JsonIgnore] public override string? Subgroup => "Users";



    // private



    private readonly MenuSlot _setting;
    private readonly ValueSlot<string> _user;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // picking a setting also picks its type below (when the slot it's in can take it)
    public static List<string> GetSettingMenuItems() => TwitchInfos.Names(TwitchInfos.User);

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double", "Bool"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double, ValueKind.Bool];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // menu events



    // picking an option in the dropdown (not a block in it: that's only known when it runs)
    private void OnSettingPicked()
    {
        var value = _setting.Text;
        if (TwitchInfos.Find(TwitchInfos.User, value) is { } info && Kind != info.Kind) TrySetKind(info.Kind);
    }



    // json



    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("user")]
    private ValueSlotData JsonUser
    {
        get => _user.GetData();
        set => _user.LoadData(value);
    }



    // constructor



    public GetTwitchUserBlock() : base(ValueKind.Int)
    {
        _setting = new("Setting", GetSettingMenuItems, "Account Age Days");
        _user = new("User");
        _slots = [_setting, _user];
        _user.SetText("{userLogin}");

        _setting.TextChanged += OnSettingPicked;
        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var setting = await _setting.EvaluateAsync(context);
        var info = TwitchInfos.Find(TwitchInfos.User, setting) ?? throw Fail($"unknown setting '{setting}'");
        var user = await _user.EvaluateAsync(context);
        var twitch = Api(context).Twitch;

        object value;
        if (info.Name is "Last Category" or "Last Title")
        {
            // what their channel is set to: kept even while they're offline (a shoutout's "last playing")
            var channel = await twitch.GetChannelAsync(user);
            value = info.Name == "Last Category" ? channel.Category : channel.Title;
        }
        else
        {
            var found = await twitch.GetUserAsync(user) ?? throw Fail($"there's no twitch user '{user.Trim().TrimStart('@').ToLowerInvariant()}'");
            value = info.Name switch
            {
                "Display Name" => found.DisplayName,
                "Id" => found.Id,
                "Account Age Days" => (int)(DateTime.UtcNow - found.CreatedAt).TotalDays,
                "Created On" => found.CreatedAt.ToString("yyyy-MM-dd"),
                "Description" => found.Description,
                _ => throw Fail($"unknown setting '{info.Name}'"),
            };
        }

        if (!ValueTypes.TryConvert(value, info.Kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }
}
