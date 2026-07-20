using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public class DeviceMenuItem(string Label, InputDeviceModel? Model)
{
    public string Label { get; } = Label;
    public InputDeviceModel? Model = Model;
}