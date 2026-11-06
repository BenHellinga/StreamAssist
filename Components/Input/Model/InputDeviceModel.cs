using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.Model;



// what an input device is: a box's layout takes one kind (its dropdown only lists devices of that kind). A microphone
// is any audio input Windows has (a mic, or e.g. a Voicemeeter output)
public enum DeviceKind { Keyboard, Mouse, Controller, Microphone }



// contains data for a single input device (a physical keyboard or mouse from raw input, or an XInput controller)
public class InputDeviceModel : ModelBase
{
    // private



    private string _devicePath;
    private string _productName;
    private string _manufacturerName;
    private int _productId;
    private int _vendorId;
    private int _number;
    private DeviceKind _kind;



    // public



    public string DevicePath => _devicePath; // what a box saves (an XInput controller's is "xinput:1" to "xinput:4")
    public string ProductName => _productName;
    public string ManufacturerName => _manufacturerName;
    public int ProductId => _productId;
    public int VendorId => _vendorId;
    public DeviceKind Kind => _kind;



    // constructor



    public InputDeviceModel(string devicePath, string productName, string manufacturerName, int productId, int vendorId, DeviceKind kind = DeviceKind.Keyboard)
    {
        _devicePath = devicePath;
        _productName = productName;
        _manufacturerName = manufacturerName;
        _productId = productId;
        _vendorId = vendorId;
        _number = 1;
        _kind = kind;
    }



    // public api



    // a controller is just its slot ("Controller 1"), a microphone its Windows name, keyboards and mice their name,
    // maker and ids
    public string GetDisplayLabel()
    {
        if (_kind is DeviceKind.Controller or DeviceKind.Microphone) return _productName;

        string productName = string.IsNullOrWhiteSpace(ProductName) ? "Unknown" : ProductName;
        string manufacturerName = string.IsNullOrWhiteSpace(ManufacturerName) ? "Unknown" : ManufacturerName;
        var label = $"{productName} - {manufacturerName} - {VendorId:X4}:{ProductId:X4}";
        return _number > 1 ? $"{label} #{_number}" : label;
    }



    // with identical devices plugged in, which one this is (1 = the first, its label has no number)
    public void SetNumber(int number) => _number = number;



    // disposal



    public override void Dispose() {}
}
