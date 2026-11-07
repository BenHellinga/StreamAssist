using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Shell;

namespace StreamAssist.Components.OBS.Values;



public class GetObsOutputBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get OBS Output";
    [JsonIgnore] public override string Description => "Reads one of OBS's outputs (Streaming, Recording, Replay Buffer, Virtual Camera): Active is whether it's running, Time how long it has been as h:mm:ss, e.g. 2:00:00, like OBS's own counter (empty while it's off; only Streaming and Recording have one, a recording's paused time isn't counted). Picking a setting picks its type.";
    [JsonIgnore] public override string? Subgroup => "Outputs";



    // private



    private readonly MenuSlot _output;
    private readonly MenuSlot _setting;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    public static List<string> GetOutputMenuItems() => [.. ObsManager.Outputs];

    // picking a setting also picks its type below (when the slot it's in can take it)
    public static List<string> GetSettingMenuItems() => ["Active", "Time"];

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Bool", "Text"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Bool, ValueKind.Text];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // menu events



    // picking an option in the dropdown (not a block in it: that's only known when it runs)
    private void OnSettingPicked()
    {
        var kind = _setting.Text == "Time" ? ValueKind.Text : ValueKind.Bool;
        if (Kind != kind) TrySetKind(kind);
    }



    // json



    [JsonInclude] [JsonPropertyName("output")]
    private ValueSlotData JsonOutput
    {
        get => _output.GetData();
        set => _output.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("setting")]
    private ValueSlotData JsonSetting
    {
        get => _setting.GetData();
        set => _setting.LoadData(value);
    }



    // constructor



    public GetObsOutputBlock() : base(ValueKind.Bool)
    {
        _output = new("Output", GetOutputMenuItems, "Streaming");
        _setting = new("Setting", GetSettingMenuItems, "Active");
        _slots = [_output, _setting];

        _setting.TextChanged += OnSettingPicked;
        TrackSlots();
    }



    // compute



    protected override async Task<object> ComputeAsync(TriggerContext context)
    {
        var output = await _output.EvaluateAsync(context);
        var setting = await _setting.EvaluateAsync(context);
        var obs = GlobalContext.ObsManager ?? throw Fail("obs not set up");

        var (ok, status) = await obs.TryGetOutputAsync(output);
        if (!ok) throw new BlockFailedException($"{output.ToLowerInvariant()} not read");

        object value = setting == "Time"
            ? status.Active ? Format(status.Time) : ""
            : status.Active;
        var kind = setting == "Time" ? ValueKind.Text : ValueKind.Bool;
        if (!ValueTypes.TryConvert(value, kind, Kind, out var result, out var error)) throw Fail(error);
        return result;
    }



    // helpers



    // h:mm:ss, hours not limited to a day (e.g. 27:03:09)
    public static string Format(TimeSpan time) => $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
}
