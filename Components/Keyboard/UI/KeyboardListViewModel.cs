using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // private



    private readonly KeyboardListModel _listModel;
    private readonly KeyboardInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;
    private ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private ObservableCollection<ConfigMenuItem> _configMenuItems;
    private ObservableCollection<KeyboardSlotViewModel> _slots;



    // public



    public ObservableCollection<KeyboardSlotViewModel> Slots => _slots;



    // constructor



    public KeyboardListViewModel(KeyboardListModel listModel, KeyboardInputManager inputManager, KeybindViewModel? keyConfigurator = null)
    {
        _listModel = listModel;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;

        _deviceMenuItems = [];
        _configMenuItems = [];
        _slots = [];

        RefreshDevices();
        RefreshConfigs();

        foreach (var slot in _listModel.Slots)
            AddSlot(slot);

        SubscribeToKeyboardListModel(_listModel);
    }



    // subscription helpers



    private void SubscribeToKeyboardListModel(KeyboardListModel model)
    {
        model.SlotAdded += OnSlotAdded;
        model.SlotRemoved += OnSlotRemoved;
        model.ConfigAdded += OnConfigCreated;
        model.ConfigRemoved += OnConfigDeleted;
        model.DeviceListUpdated += OnDeviceListUpdated;
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel model)
    {
        model.SlotAdded -= OnSlotAdded;
        model.SlotRemoved -= OnSlotRemoved;
        model.ConfigAdded -= OnConfigCreated;
        model.ConfigRemoved -= OnConfigDeleted;
        model.DeviceListUpdated -= OnDeviceListUpdated;
    }



    private void SubscribeToKeyboardSlotViewModel(KeyboardSlotViewModel viewModel)
    {
        viewModel.AddConfigRequested += OnAddConfigRequested;
        viewModel.RemoveConfigRequested += OnRemoveConfigRequested;
    }



    private void UnsubscribeFromKeyboardSlotViewModel(KeyboardSlotViewModel viewModel)
    {
        viewModel.AddConfigRequested -= OnAddConfigRequested;
        viewModel.RemoveConfigRequested -= OnRemoveConfigRequested;
    }



    // ui events



    [RelayCommand] private void OnAddSlotRequested() => _listModel.AddSlot();



    [RelayCommand] private void OnRemoveSlotRequested() => _listModel.TryRemoveLastSlot();



    // model events



    private void OnSlotAdded(KeyboardSlotModel model) => AddSlot(model);



    private void OnSlotRemoved(KeyboardSlotModel model) => RemoveSlot(model);



    private void OnConfigCreated(KeybindConfigModel config) => _configMenuItems.Add(new(config.Name, config));



    private void OnConfigDeleted(KeybindConfigModel config)
    {
        var menuItem = _configMenuItems.FirstOrDefault(c => c.Model == config);
        if (menuItem == null) return;
        _configMenuItems.Remove(menuItem);
    }



    private void OnDeviceListUpdated() => RefreshDevices(); // TODO fix - will reset all selected devices



    // other events



    private void OnAddConfigRequested(string name, Dictionary<ushort, KeybindModel>? bindings, Action<KeybindConfigModel>? callback)
    {
        var configModel = _listModel.TryAddConfig(name, bindings);
        if (configModel == null) return;
        callback?.Invoke(configModel);
    }



    private void OnRemoveConfigRequested(KeybindConfigModel model) => _listModel.TryRemoveConfig(model);



    // helpers



    private void AddSlot(KeyboardSlotModel model)
    {
        var viewModel = new KeyboardSlotViewModel(model, _inputManager, _deviceMenuItems, _configMenuItems, _keyConfigurator);
        _slots.Add(viewModel);
        SubscribeToKeyboardSlotViewModel(viewModel);
    }



    private void RemoveSlot(KeyboardSlotModel model)
    {
        var slot = _slots.FirstOrDefault(s => s.RepresentsModel(model));
        if (slot is null) return;

        UnsubscribeFromKeyboardSlotViewModel(slot);
        _slots.Remove(slot);
        _keyConfigurator?.DeselectKeys(slot.Keys);
        slot.Dispose();
    }



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
        UnsubscribeFromKeyboardListModel(_listModel);

        foreach (var slot in _slots)
        {
            UnsubscribeFromKeyboardSlotViewModel(slot);
            slot.Dispose();
        }
    }
}