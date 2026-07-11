using System.Text.Json.Serialization;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardComponentModel
{
    [JsonPropertyName("keyboardList")]
    public KeyboardListModel KeyboardList { get; set; } = new();



    [JsonPropertyName("actionListWidth")]
    public int actionListWidth = 100; // dont do anything yet



    [JsonPropertyName("actionPanelHeight")]
    public int actionListHeight = 100; // dont do anything yet
}