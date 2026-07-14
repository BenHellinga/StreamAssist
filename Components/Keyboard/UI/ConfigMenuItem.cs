using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public class ConfigMenuItem(string Label, KeybindConfigModel? Model)
{
    public string Label { get; } = Label;
    public KeybindConfigModel? Model = Model;
}