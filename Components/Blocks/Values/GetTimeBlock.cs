using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Blocks.Values;



// the date and time right now (this PC's clock): one part of it as a number, or the date / time / day as text. Picking
// a setting picks its type, like Get Stream Info
public class GetTimeBlock : AdaptableValueBlock
{
    // info



    [JsonIgnore] public override string Name => "Get Time";
    [JsonIgnore] public override string Description => "The date and time right now, on this PC: Year, Month, Day, Hour (0-23), Minute, Second, Millisecond, Unix Seconds (since 1970) as whole numbers, or Date (2026-10-30), Time (19:55:56) or Day of Week (Friday) as text. Picking a setting picks its type.";
    [JsonIgnore] public override BlockGroup Group => BlockGroup.Text;



    // private



    private static readonly IReadOnlyList<(string Name, ValueKind Kind, Func<DateTime, object> Read)> Settings =
    [
        ("Date", ValueKind.Text, now => now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        ("Time", ValueKind.Text, now => now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
        ("Day of Week", ValueKind.Text, now => now.DayOfWeek.ToString()),
        ("Year", ValueKind.Int, now => now.Year),
        ("Month", ValueKind.Int, now => now.Month),
        ("Day", ValueKind.Int, now => now.Day),
        ("Hour", ValueKind.Int, now => now.Hour),
        ("Minute", ValueKind.Int, now => now.Minute),
        ("Second", ValueKind.Int, now => now.Second),
        ("Millisecond", ValueKind.Int, now => now.Millisecond),
        ("Unix Seconds", ValueKind.Int, now => (int)Math.Min(int.MaxValue, new DateTimeOffset(now).ToUnixTimeSeconds())),
    ];

    private string _setting;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    // picking a setting also picks its type below (when the slot it's in can take it)
    [ConfigurableParameter("Setting", ConfiguratorType.Menu)] [JsonPropertyName("setting")]
    public string Setting
    {
        get => _setting;
        set
        {
            SetParameter(ref _setting, value);
            if (Find(value) is { } setting && Kind != setting.Kind) TrySetKind(setting.Kind);
        }
    }

    public static List<string> GetSettingMenuItems() => Settings.Select(s => s.Name).ToList();

    // what it's read as, placing it in a slot picks the slot's type (saved after the setting, so a loaded block keeps it)
    [ConfigurableParameter("As", ConfiguratorType.Menu)] [JsonPropertyName("kind")]
    public string As
    {
        get => KindName;
        set => KindName = value;
    }

    public static List<string> GetAsMenuItems() => ["Text", "Int", "Double"];

    [JsonIgnore] public override IReadOnlyList<ValueKind> AllowedKinds => [ValueKind.Text, ValueKind.Int, ValueKind.Double];
    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // constructor



    public GetTimeBlock() : base(ValueKind.Text)
    {
        _setting = "Date";
        _slots = [];

        TrackSlots();
    }



    // compute



    protected override Task<object> ComputeAsync(TriggerContext context)
    {
        var setting = Find(_setting) ?? throw Fail($"unknown setting '{_setting}'");
        var value = setting.Read(DateTime.Now);

        if (!ValueTypes.TryConvert(value, setting.Kind, Kind, out var result, out var error)) throw Fail(error);
        return Task.FromResult(result);
    }



    // helpers



    private static (string Name, ValueKind Kind, Func<DateTime, object> Read)? Find(string name) =>
        Settings.Any(s => s.Name == name) ? Settings.First(s => s.Name == name) : null;
}
