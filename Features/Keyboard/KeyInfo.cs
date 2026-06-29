using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// One key cap on the visual keyboard grid.
/// </summary>
public partial class KeyInfo(string Label, ushort VK, double X, double Y, double W, double H) : ObservableObject
{
    public string Label { get; init; } = Label;
    public ushort VK    { get; init; } = VK;
    public double X     { get; init; } = X;
    public double Y     { get; init; } = Y;
    public double W     { get; init; } = W;
    public double H     { get; init; } = H;
 
    [ObservableProperty] private bool   _isPressed      = false;
    [ObservableProperty] private bool   _hasBoundAction = false;
    [ObservableProperty] private string _boundActionName = "";
}