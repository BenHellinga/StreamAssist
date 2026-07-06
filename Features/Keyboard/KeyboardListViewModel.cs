using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardListViewModel : ViewModelBase
{
    private readonly InputManager _inputManager;
    private readonly KeyboardFeatureSettings _settings;
    private readonly ActionPanelViewModel _actionPanel;

    public ObservableCollection<KeyboardDisplayViewModel> Slots { get; } = [];



    public KeyboardListViewModel(InputManager inputManager, KeyboardFeatureSettings settings, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _settings = settings;
        _actionPanel = actionPanel;

        var savedSlots = _settings.RootData.Slots;
        if (savedSlots.Count == 0)
            AddSlotInternal(null);
        else
            foreach (var slotData in savedSlots)
                AddSlotInternal(slotData);
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
        var slot = new KeyboardDisplayViewModel(_inputManager, _settings, initialData);
        slot.KeySelected += _actionPanel.ShowKey;
        slot.PropertyChanged += OnSlotPropertyChanged;

        Slots.Add(slot);
    }



    private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(KeyboardDisplayViewModel.SelectedDevice)
                            or nameof(KeyboardDisplayViewModel.SelectedConfigName))
            PersistSlots();
    }



    private void PersistSlots()
    {
        _settings.RootData.Slots.Clear();
        foreach (var slot in Slots)
            _settings.RootData.Slots.Add(slot.CaptureState());

        _settings.Save();
    }
}