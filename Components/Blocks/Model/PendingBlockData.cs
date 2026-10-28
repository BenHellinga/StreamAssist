using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Blocks.Model;



// how a block is saved: its class name plus its own data (which includes any nested blocks)
public class PendingBlockData
{
    [JsonPropertyName("type")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}
