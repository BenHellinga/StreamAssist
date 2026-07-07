using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Keyboard;



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
    [ObservableProperty] private IStreamAction? _boundAction;



    partial void OnBoundActionChanged(IStreamAction? value)
    {
        HasBoundAction  = value is not null;
        BoundActionName = value?.Name ?? "";
    }
}
