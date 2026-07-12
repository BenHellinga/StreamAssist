using System.Text.Json;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.AppAction.Model;



public class PendingActionData
{
    [JsonPropertyName("type")]
    public string TypeName { get; set; } = "";

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}