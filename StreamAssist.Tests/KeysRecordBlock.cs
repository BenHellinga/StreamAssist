using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Tests;

public class KeysRecordBlock : ActionBlock
{
    public static string[]? Seen;
    [JsonIgnore] public override string Name => "KeysRecord";
    [JsonIgnore] public override string Description => "test";
    protected override Task ExecuteAsync(TriggerContext context)
    {
        Seen = context.Parameters.Keys.ToArray();
        return Task.CompletedTask;
    }
}
