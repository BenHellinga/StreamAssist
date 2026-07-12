using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class DisplayKey(string Label, ushort VK, double X, double Y, double W, double H) : ObservableObject
{
    public string Label { get; init; } = Label;
    public ushort VK { get; init; } = VK;
    public double X { get; init; } = X;
    public double Y { get; init; } = Y;
    public double W { get; init; } = W;
    public double H { get; init; } = H;

    [ObservableProperty] private bool _isPressed = false;
    [ObservableProperty] private bool _isBound = false;
}
