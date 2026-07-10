using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public record ConfigMenuItem(string Label, KeyboardConfigModel? Model)
{
    public string Label { get; } = Label;
    public KeyboardConfigModel? Model = Model;
}