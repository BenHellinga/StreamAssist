using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Tests;

public class ChatLogBlock : ActionBlock
{
    public static List<string> Log = [];
    [JsonIgnore] public override string Name => "ChatLog";
    [JsonIgnore] public override string Description => "test";
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
    protected override Task ExecuteAsync(TriggerContext context)
    {
        string P(string k) => context.Parameters.TryGetValue(k, out var v) ? v : "";
        Log.Add($"{Tag}|rest={P("rest")}|user={P("user")}|amount={P("amount")}|target={P("target")}");
        return Task.CompletedTask;
    }
}
