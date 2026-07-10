namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardDeviceModel(string DevicePath, string ProductName, string ManufacturerName, int ProductId, int VendorId)
{
    public string DevicePath { get; init; } = DevicePath;
    public string ProductName { get; init; } = ProductName;
    public string ManufacturerName { get; init; } = ManufacturerName;
    public int ProductId { get; init; } = ProductId;
    public int VendorId { get; init; } = VendorId;

    public string DisplayLabel
    {
        get
        {
            string productName = string.IsNullOrWhiteSpace(ProductName) ? "Unknown" : ProductName;
            string manufacturerName = string.IsNullOrWhiteSpace(ManufacturerName) ? "Unknown" : ManufacturerName;
            return $"{productName} - {manufacturerName} - {VendorId:X4}:{ProductId:X4}";
        }
    }
}