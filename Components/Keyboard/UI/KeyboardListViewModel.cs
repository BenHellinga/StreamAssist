using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // dependencies
    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel;
    private readonly ActionPanelViewModel _actionPanel;

    // display collections
    public ObservableCollection<DeviceMenuItem> DeviceMenuItems { get; }
    public ObservableCollection<ConfigMenuItem> ConfigMenuItems { get; }
    public ObservableCollection<KeyboardSlotViewModel> Slots { get; }



    // constructor



    public KeyboardListViewModel(KeyboardInputManager inputManager, KeyboardListModel listModel, ActionPanelViewModel actionPanel)
    {
        _inputManager = inputManager;
        _listModel = listModel;
        _actionPanel = actionPanel;

        DeviceMenuItems = [];
        ConfigMenuItems = [];
        Slots = [];

        RefreshDevices();
        RefreshConfigs();

        foreach (var slot in _listModel.Slots)
            Slots.Add(new KeyboardSlotViewModel(this, _inputManager, _listModel, _actionPanel, slot));

        _listModel.SlotAdded += OnSlotAdded;
        _listModel.SlotRemoved += OnSlotRemoved;
        _listModel.ConfigListUpdated += OnConfigListUpdated;
        _listModel.DeviceListUpdated += OnDeviceListUpdated;
    }



    // button callbacks



    [RelayCommand]
    private void OnAddSlotRequested() => _listModel.AddSlot();



    [RelayCommand]
    private void OnRemoveSlotRequested() => _listModel.TryRemoveLastSlot();



    // model callbacks



    private void OnSlotAdded(KeyboardSlotModel slot) => Slots.Add(new KeyboardSlotViewModel(this, _inputManager, _listModel, _actionPanel, slot));



    private void OnSlotRemoved(KeyboardSlotModel slotModel)
    {
        var slot = Slots.FirstOrDefault(s => s.SlotModel == slotModel);
        if (slot is null) return;

        Slots.Remove(slot);
        slot.Dispose();
    }



    private void OnConfigListUpdated() => RefreshConfigs();



    private void OnDeviceListUpdated() => RefreshDevices();



    // refresh methods



    private void RefreshDevices()
    {
        DeviceMenuItems.Clear();
        DeviceMenuItems.Add(new(KeyboardListModel.NoneDevice, null));
        foreach (var device in _listModel.Devices)
            DeviceMenuItems.Add(new(device.DisplayLabel, device));
    }



    private void RefreshConfigs()
    {
        var desired = new List<ConfigMenuItem> { new(KeyboardListModel.NoneConfig, null) };
        desired.AddRange(_listModel.Configs.Select(c => new ConfigMenuItem(c.Name, c)));

        for (int i = ConfigMenuItems.Count - 1; i >= 0; i--)
            if (!desired.Any(d => d.Model == ConfigMenuItems[i].Model))
                ConfigMenuItems.RemoveAt(i);

        foreach (var item in desired)
            if (!ConfigMenuItems.Any(c => c.Model == item.Model))
                ConfigMenuItems.Add(item);
    }



    // disposal



    public void Dispose()
    {
        _listModel.SlotAdded -= OnSlotAdded;
        _listModel.SlotRemoved -= OnSlotRemoved;
        _listModel.ConfigListUpdated -= OnConfigListUpdated;
        _listModel.DeviceListUpdated -= OnDeviceListUpdated;

        foreach (var slot in Slots)
            slot.Dispose();
    }
}