using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Owns the full set of keyboard slots — the entire middle area of the
/// Keyboard tab. The "+" button adds a new independent slot; "−" removes
/// the most recently added one (at least one always remains).
/// </summary>
public partial class KeyboardListViewModel : ViewModelBase
{
    private readonly InputManager _inputManager;

    // Reserved for the persistence rework landing next round — not yet
    // read from or written to.
    private readonly KeyboardFeatureSettings _settings;

    private readonly ActionPanelViewModel _actionPanel;

    public ObservableCollection<KeyboardSlotViewModel> Slots { get; } = [];



    public KeyboardListViewModel(InputManager inputManager, KeyboardFeatureSettings settings, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _settings = settings;
        _actionPanel = actionPanel;

        AddSlotInternal(); // always start with one keyboard
    }



    [RelayCommand]
    private void AddKeyboard()
    {
        AddSlotInternal();
        RemoveKeyboardCommand.NotifyCanExecuteChanged();
    }



    [RelayCommand(CanExecute = nameof(CanRemoveKeyboard))]
    private void RemoveKeyboard()
    {
        if (Slots.Count <= 1) return;

        var last = Slots[^1];
        Slots.RemoveAt(Slots.Count - 1);
        last.Dispose();

        RemoveKeyboardCommand.NotifyCanExecuteChanged();
    }



    private bool CanRemoveKeyboard() => Slots.Count > 1;



    private void AddSlotInternal()
    {
        var slot = new KeyboardSlotViewModel(_inputManager);
        slot.KeyboardDisplay.KeySelected += _actionPanel.ShowKey;
        Slots.Add(slot);
    }
}