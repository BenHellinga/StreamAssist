using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// Owns the full set of keyboard slots — the entire middle area of the
/// Keyboard tab. Restores saved slots (device + config picks) on startup;
/// the "+" button adds a new independent slot, "−" removes the most
/// recently added one (at least one always remains). The slot count and
/// each slot's picks are persisted to keyboard.json on every change.
/// </summary>
public partial class KeyboardListViewModel : ViewModelBase
{
    private readonly InputManager _inputManager;
    private readonly KeyboardFeatureSettings _settings;
    private readonly ActionPanelViewModel _actionPanel;

    public ObservableCollection<KeyboardSlotViewModel> Slots { get; } = [];

    /// <summary>
    /// Assumes _settings.Load() has already been called by app startup,
    /// before this is constructed.
    /// </summary>
    public KeyboardListViewModel(InputManager inputManager, KeyboardFeatureSettings settings, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _settings = settings;
        _actionPanel = actionPanel;

        var savedSlots = _settings.RootData.Slots;
        if (savedSlots.Count == 0)
        {
            AddSlotInternal(null); // fresh install — always start with one keyboard
        }
        else
        {
            foreach (var slotData in savedSlots)
                AddSlotInternal(slotData);
        }
    }



    [RelayCommand]
    private void AddKeyboard()
    {
        AddSlotInternal(null);
        RemoveKeyboardCommand.NotifyCanExecuteChanged();
        PersistSlots();
    }



    [RelayCommand(CanExecute = nameof(CanRemoveKeyboard))]
    private void RemoveKeyboard()
    {
        if (Slots.Count <= 1) return;

        var last = Slots[^1];
        Slots.RemoveAt(Slots.Count - 1);
        last.Dispose();

        RemoveKeyboardCommand.NotifyCanExecuteChanged();
        PersistSlots();
    }



    private bool CanRemoveKeyboard() => Slots.Count > 1;



    private void AddSlotInternal(KeyboardSlotData? initialData)
    {
        var slot = new KeyboardSlotViewModel(_inputManager, _settings, initialData);
        slot.KeyboardDisplay.KeySelected += _actionPanel.ShowKey;

        // Re-persist this slot's picks whenever they change.
        slot.KeyboardMenu.DeviceChanged += _ => PersistSlots();
        slot.KeyboardMenu.ConfigChanged += _ => PersistSlots();

        Slots.Add(slot);
    }



    private void PersistSlots()
    {
        _settings.RootData.Slots.Clear();
        foreach (var slot in Slots)
            _settings.RootData.Slots.Add(slot.CaptureState());

        _settings.Save();
    }
}