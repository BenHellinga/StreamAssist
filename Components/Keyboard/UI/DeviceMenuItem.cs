using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// data class for a single device menu item
public class DeviceMenuItem
{
    // private



    private string _label;
    private InputDeviceModel? _device;



    // public



    public string Label => _label;
    public InputDeviceModel? Model => _device;



    // constructor



    public DeviceMenuItem(string label, InputDeviceModel? device)
    {
        _label = label;
        _device = device;
    }
}