using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

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

    // The actual action to run when this key is pressed. Setting this keeps
    // HasBoundAction / BoundActionName in sync automatically, since those
    // two exist for the UI to bind against without needing IFeatureAction.
    [ObservableProperty] private IFeatureAction? _boundAction;

    partial void OnBoundActionChanged(IFeatureAction? value)
    {
        HasBoundAction  = value is not null;
        BoundActionName = value?.Name ?? "";
    }
}
