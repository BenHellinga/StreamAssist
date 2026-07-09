using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Config;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // public static constants
    public static readonly KeyboardInfo NoneDevice = new("None", "None", "", 0, 0);
    public static readonly string NoneConfig = KeyboardListModel.NoneConfig;

    // dependencies
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listData;
    private readonly ActionPanelViewModel _actionPanel;
    private readonly CallbackBlocker _callbackBlocker;

    // display collections
    public ObservableCollection<KeyboardInfo> AllDevices { get; }
    public ObservableCollection<string> AllConfigs { get; }
    public ObservableCollection<KeyboardSlotViewModel> Slots { get; }



    // constructor



    public KeyboardListViewModel(KeyboardInputManager inputManager, KeyboardListModel listData, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _listData = listData;
        _actionPanel = actionPanel;
        _callbackBlocker = new();

        _callbackBlocker.Block();

        AllDevices = [];
        AllConfigs = [];
        Slots = [];

        RefreshDevices();
        RefreshConfigs();

        if (_listData.Slots.Count == 0) // TODO should be moved to KeyboardListModel
            _listData.AddSlot();

        foreach (var slotData in _listData.Slots)
            Slots.Add(CreateSlotViewModel(slotData));

        _listData.SlotAdded += OnSlotAdded;
        _listData.SlotRemoved += OnSlotRemoved;
        _listData.ConfigListUpdated += OnConfigListUpdated;

        _callbackBlocker.Unblock();
    }



    // button callbacks



    [RelayCommand]
    private void OnAddKeyboard() => _listData.AddSlot();

    [RelayCommand]
    private void OnRemoveKeyboard() => _listData.RemoveLastSlot();



    // model callbacks



    private void OnSlotAdded(KeyboardSlotModel slotData) => Slots.Add(CreateSlotViewModel(slotData));

    private void OnSlotRemoved(KeyboardSlotModel slotData)
    {
        var slot = Slots.FirstOrDefault(s => s.SlotModel == slotData);
        if (slot is null) return;

        Slots.Remove(slot);
        slot.KeySelected -= _actionPanel.ShowKey;
        slot.Dispose();
    }

    private void OnConfigListUpdated() => RefreshConfigs();



    // slot creation



    private KeyboardSlotViewModel CreateSlotViewModel(KeyboardSlotModel slotData)
    {
        var slot = new KeyboardSlotViewModel(_inputManager, _listData, slotData, AllDevices, AllConfigs, _callbackBlocker);
        slot.KeySelected += _actionPanel.ShowKey;
        return slot;
    }



    // refresh methods



    private void RefreshDevices()
    {
        AllDevices.Clear();
        AllDevices.Add(NoneDevice);
        foreach (var d in KeyboardInputManager.GetAvailableKeyboards())
            AllDevices.Add(d);
    }



    private void RefreshConfigs()
    {
        AllConfigs.Clear();
        AllConfigs.Add(NoneConfig);
        foreach (var name in _listData.Configs.Select(c => c.Name).OrderBy(n => n))
            AllConfigs.Add(name);
    }



    // disposal



    public void Dispose()
    {
        _listData.SlotAdded -= OnSlotAdded;
        _listData.SlotRemoved -= OnSlotRemoved;
        _listData.ConfigListUpdated -= OnConfigListUpdated;

        foreach (var slot in Slots)
        {
            slot.KeySelected -= _actionPanel.ShowKey;
            slot.Dispose();
        }
    }
}