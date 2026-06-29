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
    public KeyboardInputManager KeyboardInputManager { get; } = new();

    // public AudioService   AudioService   { get; } = new();
    // public ObsService     ObsService     { get; } = new();
    // public ChatbotService ChatbotService { get; } = new();

    // ── Feature ViewModels (consume services + settings) ──────────────────
    public KeyboardViewModel KeyboardViewModel { get; }

    // public AudioViewModel   AudioViewModel   { get; }
    // public ObsViewModel     ObsViewModel     { get; }
    // public ChatbotViewModel ChatbotViewModel { get; }

    public MainWindowViewModel()
    {
        // Load all settings from disk before constructing ViewModels
        KeyboardSettings.Load();
        // ChatbotSettings.Load();
        // ObsSettings.Load();

        // Construct ViewModels, injecting both the service and its settings
        KeyboardViewModel = new KeyboardViewModel(KeyboardInputManager, KeyboardSettings);

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
