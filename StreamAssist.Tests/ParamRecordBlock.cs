using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Tests;

public class ParamRecordBlock : ActionBlock
{
    public static string? Seen;
    [JsonIgnore] public override string Name => "ParamRecord";
    [JsonIgnore] public override string Description => "test";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    protected override Task ExecuteAsync(TriggerContext context)
    {
        Seen = context.Parameters.TryGetValue(Key, out var v) ? v : "(missing)";
        return Task.CompletedTask;
    }
}
