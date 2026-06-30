using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the KeyboardTab view. Composition root for the keyboard feature -
/// owns the shared InputManager/KeyboardFeatureSettings and hands them off
/// to KeyboardList (which owns the per-keyboard slots), plus the
/// feature-wide ActionList and the shared ActionPanel.
/// </summary>
public partial class KeyboardTabViewModel : ViewModelBase
{
    public KeyboardListViewModel KeyboardList { get; }
    public ActionListViewModel ActionList { get; } = new();
    public ActionPanelViewModel ActionPanel { get; } = new();

    // Bound to the GridSplitter column in KeyboardTab.axaml. Dragging the
    // splitter updates this directly (two-way bound), so the resize sticks
    // for the rest of the session — same pattern as MainWindowViewModel's
    // ConsoleWidth.
    [ObservableProperty]
    private GridLength _actionListWidth = new(300);

    // Same pattern, but for the row split between KeyboardList and
    // ActionPanel.
    [ObservableProperty]
    private GridLength _actionPanelHeight = new(160);



    // constructor



    public KeyboardTabViewModel(InputManager inputManager, KeyboardFeatureSettings settings)
    {
        KeyboardList = new KeyboardListViewModel(inputManager, settings, ActionPanel);
    }
}