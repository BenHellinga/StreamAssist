using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardConfigModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("keyBindings")]
    public Dictionary<ushort, string> Keybindings { get; set; }



    public KeyboardConfigModel()
    {
        Name = "null";
        Keybindings = [];
    }



    public KeyboardConfigModel(string name, Dictionary<ushort, string>? keybindings = null)
    {
        Name = name;
        Keybindings = keybindings ?? [];
    }
}