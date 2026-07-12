using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



public class DeviceMenuItem(string Label, KeyboardDeviceModel? Model)
{
    public string Label { get; } = Label;
    public KeyboardDeviceModel? Model = Model;
}