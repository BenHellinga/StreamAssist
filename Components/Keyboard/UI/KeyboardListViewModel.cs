using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.AppAction;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // dependencies
    private readonly KeyboardListModel _listModel;
    private readonly KeyConfiguratorViewModel _keyConfigurator;
    private readonly KeyboardInputManager _inputManager;

    // private storage fields
    private ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private ObservableCollection<ConfigMenuItem> _configMenuItems;
    private ObservableCollection<KeyboardSlotViewModel> _slots;

    // public getter properties
    public ObservableCollection<DeviceMenuItem> DeviceMenuItems => _deviceMenuItems;
    public ObservableCollection<ConfigMenuItem> ConfigMenuItems => _configMenuItems;
    public ObservableCollection<KeyboardSlotViewModel> Slots => _slots;



    // constructor



    public KeyboardListViewModel(KeyboardListModel listModel, KeyConfiguratorViewModel keyConfigurator, KeyboardInputManager inputManager)
    {
        _listModel = listModel;
        _keyConfigurator = keyConfigurator;
        _inputManager = inputManager;

        _deviceMenuItems = [];
        _configMenuItems = [];
        _slots = [];

        RefreshDevices();
        RefreshConfigs();

        foreach (var slot in _listModel.Slots)
            _slots.Add(new KeyboardSlotViewModel(this, _listModel, slot, _keyConfigurator, _inputManager));

        _listModel.SlotAdded += OnSlotAdded;
        _listModel.SlotRemoved += OnSlotRemoved;
        _listModel.ConfigCreated += OnConfigCreated;
        _listModel.ConfigDeleted += OnConfigDeleted;
        _listModel.DeviceListUpdated += OnDeviceListUpdated;
    }



    // button callbacks



    [RelayCommand] private void OnAddSlotRequested() => _listModel.AddSlot();



    [RelayCommand] private void OnRemoveSlotRequested() => _listModel.TryRemoveLastSlot();



    // model callbacks



    private void OnSlotAdded(KeyboardSlotModel slot) => _slots.Add(new KeyboardSlotViewModel(this, _listModel, slot, _keyConfigurator, _inputManager));



    private void OnSlotRemoved(KeyboardSlotModel slotModel)
    {
        var slot = _slots.FirstOrDefault(s => s.SlotModel == slotModel);
        if (slot is null) return;
        _slots.Remove(slot);
        slot.Dispose();
    }



    private void OnConfigCreated(KeyboardConfigModel config) => _configMenuItems.Add(new(config.Name, config));



    private void OnConfigDeleted(KeyboardConfigModel config)
    {
        var menuItem = _configMenuItems.FirstOrDefault(c => c.Model == config);
        if (menuItem == null) return;
        _configMenuItems.Remove(menuItem);
    }



    private void OnDeviceListUpdated() => RefreshDevices(); // TODO fix - will reset all selected devices



    // refresh methods



    private void RefreshDevices()
    {
        _deviceMenuItems.Clear();
        _deviceMenuItems.Add(new(KeyboardListModel.NoneDevice, null));
        foreach (var device in _listModel.Devices ?? [])
            _deviceMenuItems.Add(new(device.DisplayLabel, device));
    }



    private void RefreshConfigs() // TODO probably not required??
    {
        var desired = new List<ConfigMenuItem> { new(KeyboardListModel.NoneConfig, null) };
        desired.AddRange(_listModel.Configs.Select(c => new ConfigMenuItem(c.Name, c)));

        for (int i = _configMenuItems.Count - 1; i >= 0; i--)
            if (!desired.Any(d => d.Model == _configMenuItems[i].Model))
                _configMenuItems.RemoveAt(i);

        foreach (var item in desired)
            if (!_configMenuItems.Any(c => c.Model == item.Model))
                _configMenuItems.Add(item);
    }



    // disposal



    public override void Dispose()
    {
        _listModel.SlotAdded -= OnSlotAdded;
        _listModel.SlotRemoved -= OnSlotRemoved;
        _listModel.ConfigCreated -= OnConfigCreated;
        _listModel.ConfigDeleted -= OnConfigDeleted;
        _listModel.DeviceListUpdated -= OnDeviceListUpdated;

        foreach (var slot in _slots)
            slot.Dispose();
    }
}