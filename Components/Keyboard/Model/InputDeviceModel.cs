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
    }



    // public api



    public string GetDisplayLabel()
    {
        string productName = string.IsNullOrWhiteSpace(ProductName) ? "Unknown" : ProductName;
        string manufacturerName = string.IsNullOrWhiteSpace(ManufacturerName) ? "Unknown" : ManufacturerName;
        return $"{productName} - {manufacturerName} - {VendorId:X4}:{ProductId:X4}";
    }



    // disposal



    public override void Dispose() {}
}