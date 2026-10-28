using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.UI;



// view model to modify a KeyboardListModel object
public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // private



    private readonly KeyboardListModel _list;
    private readonly KeyboardInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;

    private ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private ObservableCollection<ConfigMenuItem> _configMenuItems;
    private ObservableCollection<KeyboardSlotViewModel> _slots;



    // public



    public ObservableCollection<KeyboardSlotViewModel> Slots => _slots;



    // constructor



    public KeyboardListViewModel(KeyboardListModel list, KeyboardInputManager inputManager, KeybindViewModel? keyConfigurator = null)
    {
        _list = list;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;

        _deviceMenuItems = [];
        _configMenuItems = [];
        _slots = [];

        _deviceMenuItems.Add(new(KeyboardListModel.NoneDevice, null));
        foreach (var device in _list.Devices ?? [])
            _deviceMenuItems.Add(new(device.GetDisplayLabel(), device));

        _configMenuItems.Add(new(KeyboardListModel.NoneConfig, null));
        foreach (var config in _list.Configs)
            _configMenuItems.Add(new(config.Name, config));

        foreach (var slot in _list.Slots)
            CreateSlot(slot);

        SubscribeToKeyboardListModel(_list);
    }



    // ui events



    [RelayCommand] private void OnAddSlotRequested() => _list.CreateSlot(out var _);



    [RelayCommand] private void OnRemoveSlotRequested() => _list.TryDeleteSlot(_list.Slots.Last());



    // model events



    private void OnSlotCreated(KeyboardSlotModel slot) => CreateSlot(slot);



    private void OnSlotDeleted(KeyboardSlotModel slot)
    {
        var viewModel = _slots.FirstOrDefault(s => s.RepresentsModel(slot));
        if (viewModel is null) return;

        UnsubscribeFromKeyboardSlotViewModel(viewModel);
        _slots.Remove(viewModel);
        _keyConfigurator?.DeselectKeys(viewModel.Keys);
        viewModel.Dispose();
    }



    private void OnConfigCreated(KeybindConfigModel config) => _configMenuItems.Add(new(config.Name, config));



    private void OnConfigDeleted(KeybindConfigModel config)
    {
        var menuItem = _configMenuItems.FirstOrDefault(c => c.Model == config);
        if (menuItem == null) return;

        _configMenuItems.Remove(menuItem);
        // nothing to dispose since no view model for configs
    }



    // view model events



    private void OnCreateConfigRequested(string name, Dictionary<ushort, KeybindModel>? bindings, Action<KeybindConfigModel>? callback)
    {
        if (!_list.TryCreateConfig(name, bindings, out var config)) return;
        callback?.Invoke(config);
    }



    private void OnDeleteConfigRequested(KeybindConfigModel config) => _list.TryDeleteConfig(config);



    // helpers



    private void SubscribeToKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated += OnSlotCreated;
        list.SlotDeleted += OnSlotDeleted;
        list.ConfigCreated += OnConfigCreated;
        list.ConfigDeleted += OnConfigDeleted;
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated -= OnSlotCreated;
        list.SlotDeleted -= OnSlotDeleted;
        list.ConfigCreated -= OnConfigCreated;
        list.ConfigDeleted -= OnConfigDeleted;
    }



    private void SubscribeToKeyboardSlotViewModel(KeyboardSlotViewModel slot)
    {
        slot.CreateConfigRequested += OnCreateConfigRequested;
        slot.DeleteConfigRequested += OnDeleteConfigRequested;
    }



    private void UnsubscribeFromKeyboardSlotViewModel(KeyboardSlotViewModel slot)
    {
        slot.CreateConfigRequested -= OnCreateConfigRequested;
        slot.DeleteConfigRequested -= OnDeleteConfigRequested;
    }



    private void CreateSlot(KeyboardSlotModel slot)
    {
        var viewModel = new KeyboardSlotViewModel(slot, _inputManager, _deviceMenuItems, _configMenuItems, _keyConfigurator);
        _slots.Add(viewModel);
        SubscribeToKeyboardSlotViewModel(viewModel);
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeyboardListModel(_list);

        foreach (var slot in _slots)
        {
            UnsubscribeFromKeyboardSlotViewModel(slot);
            slot.Dispose();
        }
    }
}