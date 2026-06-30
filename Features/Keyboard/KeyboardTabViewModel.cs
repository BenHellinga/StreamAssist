using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the KeyboardTab view. Composition root for the keyboard feature -
/// owns the shared InputManager/KeyboardFeatureSettings and hands each off
/// to the TopMenu and KeyboardDisplay child viewmodels.
/// </summary>
public partial class KeyboardTabViewModel : ViewModelBase
{
    public TopMenuViewModel TopMenu { get; }
    public KeyboardDisplayViewModel KeyboardDisplay { get; }
    public ActionListViewModel ActionList { get; } = new();
    public ActionPanelViewModel ActionPanel { get; } = new();

    // Bound to the GridSplitter column in KeyboardTab.axaml. Dragging the
    // splitter updates this directly (two-way bound), so the resize sticks
    // for the rest of the session — same pattern as MainWindowViewModel's
    // ConsoleWidth.
    [ObservableProperty]
    private GridLength _actionListWidth = new(300);

    // Same pattern, but for the row split between KeyboardDisplay and
    // ActionPanel.
    [ObservableProperty]
    private GridLength _actionPanelHeight = new(160);



    // constructor



    public KeyboardTabViewModel(InputManager inputManager, KeyboardFeatureSettings settings)
    {
        TopMenu = new TopMenuViewModel(inputManager, settings);
        KeyboardDisplay = new KeyboardDisplayViewModel(inputManager);

        KeyboardDisplay.KeySelected += ActionPanel.ShowKey;
    }
}