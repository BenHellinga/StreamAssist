using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for a single input device
public class InputDeviceModel : ModelBase
{
    // private



    private string _devicePath;
    private string _productName;
    private string _manufacturerName;
    private int _productId;
    private int _vendorId;
    private int _number;



    // public



    public string DevicePath => _devicePath;
    public string ProductName => _productName;
    public string ManufacturerName => _manufacturerName;
    public int ProductId => _productId;
    public int VendorId => _vendorId;



    // constructor



    public InputDeviceModel(string devicePath, string productName, string manufacturerName, int productId, int vendorId)
    {
        _devicePath = devicePath;
        _productName = productName;
        _manufacturerName = manufacturerName;
        _productId = productId;
        _vendorId = vendorId;
        _number = 1;
    }



    // public api



    public string GetDisplayLabel()
    {
        string productName = string.IsNullOrWhiteSpace(ProductName) ? "Unknown" : ProductName;
        string manufacturerName = string.IsNullOrWhiteSpace(ManufacturerName) ? "Unknown" : ManufacturerName;
        var label = $"{productName} - {manufacturerName} - {VendorId:X4}:{ProductId:X4}";
        return _number > 1 ? $"{label} #{_number}" : label;
    }



    // with identical keyboards plugged in, which one this is (1 = the first, its label has no number)
    public void SetNumber(int number) => _number = number;



    // disposal



    public override void Dispose() {}
}