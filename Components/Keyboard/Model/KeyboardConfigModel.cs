using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardConfigModel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("keyBindings")]
    public Dictionary<ushort, string> KeyBindings { get; set; } = []; // will be changed eventually to include per-action data
}