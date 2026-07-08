using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Keyboard;
using StreamAssist.Components.Core;
using StreamAssist.Components.Config;

namespace StreamAssist.Components.Main;

/// <summary>
/// The top-level ViewModel. Owns all services and feature settings,
/// controlling their lifetime from app start to app close.
///
/// To add a new feature later:
///   1. Create Features/MyFeature/MyFeatureData.cs        (the settings model, a plain class)
///   2. Create Features/MyFeature/MyFeatureService.cs
///   3. Create Features/MyFeature/MyFeatureViewModel.cs
///   4. Add the service, settings, and viewmodel properties below.
///   5. Call JsonFileStore.Load&lt;MyFeatureData&gt;("myfeature") in the constructor.
///   6. Call service.Dispose() and any cleanup in Dispose().
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    // ── Feature settings (one instance per feature, loaded from its own JSON file) ───
    public KeyboardListData KeyboardSettings { get; }

    // public ChatbotListData ChatbotSettings { get; }
    // public ObsListData     ObsSettings     { get; }

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



    public MainWindowViewModel()
    {
        // Load all settings from disk before constructing ViewModels
        KeyboardSettings = JsonFileStore.Load<KeyboardListData>("keyboard");
        // ChatbotSettings = JsonFileStore.Load<ChatbotListData>("chatbot");
        // ObsSettings     = JsonFileStore.Load<ObsListData>("obs");

        // Construct ViewModels, injecting both the service and its settings
        KeyboardTabViewModel = new KeyboardTabViewModel(KeyboardInputManager, KeyboardSettings);

        Tabs.Add(new TabItemViewModel("Keyboard", KeyboardTabViewModel));
        Tabs.Add(new TabItemViewModel("test", OutputConsoleViewModel));
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