using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Features.Keyboard;
using StreamAssist.Features.Shell;

namespace StreamAssist.Features.Shell;

/// <summary>
/// The top-level ViewModel. Owns all services and feature settings,
/// controlling their lifetime from app start to app close.
///
/// To add a new feature later:
///   1. Create Features/MyFeature/MyFeatureConfig.cs      (the settings model)
///   2. Create Features/MyFeature/MyFeatureSettings.cs    (extends FeatureSettings&lt;T&gt;)
///   3. Create Features/MyFeature/MyFeatureService.cs
///   4. Create Features/MyFeature/MyFeatureViewModel.cs
///   5. Add the service, settings, and viewmodel properties below.
///   6. Call settings.Load() in the constructor.
///   7. Call service.Dispose() and any cleanup in Dispose().
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ── Feature settings (one instance per feature, owns its JSON file) ───
    public KeyboardFeatureSettings KeyboardSettings { get; } = new();

    // public ChatbotFeatureSettings ChatbotSettings { get; } = new();
    // public ObsFeatureSettings     ObsSettings     { get; } = new();

    // ── Services (own OS resources — sockets, hooks, audio devices) ───────
    public InputManager KeyboardInputManager { get; } = new();

    // public AudioService   AudioService   { get; } = new();
    // public ObsService     ObsService     { get; } = new();
    // public ChatbotService ChatbotService { get; } = new();

    // ── Feature ViewModels (consume services + settings) ──────────────────
    public KeyboardTabViewModel KeyboardTabViewModel { get; }
    public OutputConsoleViewModel OutputConsoleViewModel { get; } = new();

    // public AudioViewModel   AudioViewModel   { get; }
    // public ObsViewModel     ObsViewModel     { get; }
    // public ChatbotViewModel ChatbotViewModel { get; }

    // ── Tabs (the strip at the top of the window) ──────────────────────────
    // To add a new tab once you have another feature ready:
    //   Tabs.Add(new TabItemViewModel("Audio", AudioViewModel));
    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    // ── Layout ──────────────────────────────────────────────────────────
    // Initial console width comes from AppLog.ConsoleWidthRatio (tune it
    // there). After that, the GridSplitter in MainWindow.axaml drives this
    // directly as the user drags — it's a normal settable property, not a
    // fixed ratio, so the resize sticks for the rest of the session.
    [ObservableProperty]
    private GridLength _consoleWidth = new(AppLog.ConsoleWidthRatio, GridUnitType.Star);

    public MainWindowViewModel()
    {
        // Load all settings from disk before constructing ViewModels
        KeyboardSettings.Load();
        // ChatbotSettings.Load();
        // ObsSettings.Load();

        // Construct ViewModels, injecting both the service and its settings
        KeyboardTabViewModel = new KeyboardTabViewModel(KeyboardInputManager, KeyboardSettings);

        Tabs.Add(new TabItemViewModel("Keyboard", KeyboardTabViewModel));
        // Tabs.Add(new TabItemViewModel("Audio", AudioViewModel));
        // Tabs.Add(new TabItemViewModel("OBS", ObsViewModel));
        // Tabs.Add(new TabItemViewModel("Chatbot", ChatbotViewModel));
        SelectedTab = Tabs[0];

        AppLog.WriteLine("StreamAssist started.");

        // Example of cross-feature wiring (uncomment when you have OBS):
        // KeyboardService.KeyPressed += async vk => {
        //     if (vk == 0x7C) // F13
        //         await ObsService.SwitchSceneAsync("Gaming");
        // };
    }

    public void Dispose()
    {
        KeyboardInputManager.Dispose();
        // AudioService.Dispose();
        // ObsService.Dispose();
        // ChatbotService.Dispose();
    }
}



/// <summary>
/// One entry in the top tab strip — a title plus the viewmodel to show
/// when it's selected. Not a "feature" in its own right, so it doesn't
/// get its own axaml/view/viewmodel trio — just a plain data holder.
/// </summary>
public class TabItemViewModel(string title, object content)
{
    public string Title { get; } = title;
    public object Content { get; } = content;
}
