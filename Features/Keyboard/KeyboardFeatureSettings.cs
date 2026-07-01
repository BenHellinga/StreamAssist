using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Config;
using System;

namespace StreamAssist.Features.Keyboard;



public class KeyboardConfig
{
    [JsonPropertyName("keyBindings")]
    public Dictionary<ushort, string> KeyBindings { get; set; } = new();
}



public class KeyboardSlotData
{
    [JsonPropertyName("selectedDeviceId")]
    public string? SelectedDeviceId { get; set; }

    [JsonPropertyName("selectedConfigName")]
    public string SelectedConfigName { get; set; } = "Default";
}



public class KeyboardRootData
{
    [JsonPropertyName("slots")]
    public List<KeyboardSlotData> Slots { get; set; } = new() { new KeyboardSlotData() };
}



public class KeyboardFeatureSettings : FeatureSettings<KeyboardConfig, KeyboardRootData>
{
    public KeyboardFeatureSettings() : base("keyboard") { }

    public event Action<string>? BindingsChanged;

    public void SaveAndNotifyBindingsChanged(string configName)
    {
        Save();
        BindingsChanged?.Invoke(configName);
    }
}