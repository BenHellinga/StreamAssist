namespace StreamAssist.Features.Keyboard;



public class KeyboardInfo(string DevicePath, string ProductName, string ManufacturerName, int ProductId, int VendorId)
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
            if (ProductName == "None") return "None";

            string productName = string.IsNullOrWhiteSpace(ProductName) ? "Unknown" : ProductName;
            string manufacturerName = string.IsNullOrWhiteSpace(ManufacturerName) ? "" : ManufacturerName;
            return $"{productName} - {manufacturerName} - {VendorId:X4}:{ProductId:X4}";
        }
    }
}