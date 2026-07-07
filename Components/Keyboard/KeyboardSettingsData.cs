using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Keyboard;



public class KeyboardListData
{
    [JsonPropertyName("configs")]
    public List<KeyboardConfigData> Configs { get; set; } = [];

    [JsonPropertyName("slots")]
    public List<KeyboardSlotData> Slots { get; set; } = [];
}



public class KeyboardConfigData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("keyBindings")]
    public Dictionary<ushort, string> KeyBindings { get; set; } = new(); // will be changed eventually to include per-action data
}



public class KeyboardSlotData
{
    [JsonPropertyName("selectedDevice")]
    public string SelectedDevice { get; set; } = "None";

    [JsonPropertyName("selectedConfig")]
    public string SelectedConfig { get; set; } = "None";
}