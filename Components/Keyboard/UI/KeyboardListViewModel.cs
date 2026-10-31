using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Blocks.UI;

namespace StreamAssist.Components.Keyboard.UI;



// view model to modify a KeyboardListModel object
public partial class KeyboardListViewModel : ViewModelBase, IDisposable
{
    // private



    private readonly KeyboardListModel _list;
    private readonly KeyboardInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;

    private ObservableCollection<DeviceMenuItem> _deviceMenuItems;
    private readonly ConfigBarViewModel _configBar;
    private ObservableCollection<KeyboardSlotViewModel> _slots;



    // public



    public ObservableCollection<KeyboardSlotViewModel> Slots => _slots;
    public ConfigBarViewModel ConfigBar => _configBar; // one config for the whole tab
    public NoConfigViewModel NoConfig { get; } = new(); // shown instead of the keyboards while there's no config
    public bool HasConfig => _list.Configs.SelectedItem != null;



    // constructor



    public KeyboardListViewModel(KeyboardListModel list, KeyboardInputManager inputManager, KeybindViewModel? keyConfigurator = null)
    {
        _list = list;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;

        _deviceMenuItems = [];
        _configBar = new(_list.Configs);
        _slots = [];

        _deviceMenuItems.Add(new(KeyboardListModel.NoneDevice, null));
        foreach (var device in _list.Devices ?? [])
            _deviceMenuItems.Add(new(device.GetDisplayLabel(), device));

        foreach (var slot in _list.Slots)
            CreateSlot(slot);

        SubscribeToKeyboardListModel(_list);
        _list.Configs.ListChanged += OnConfigsChanged;
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

        _slots.Remove(viewModel);
        _keyConfigurator?.DeselectKeys(viewModel.Keys);
        viewModel.Dispose();
    }



    private void OnConfigsChanged() => OnPropertyChanged(nameof(HasConfig));



    // a keyboard was plugged in or removed: the dropdowns get the new list (edited in place, so a slot's choice isn't
    // lost while the list changes), then each slot shows its keyboard again
    private void OnDevicesChanged()
    {
        var devices = _list.Devices ?? [];
        foreach (var item in _deviceMenuItems.Where(i => i.Model != null && (!devices.Contains(i.Model) || i.Label != i.Model.GetDisplayLabel())).ToList())
            _deviceMenuItems.Remove(item);
        foreach (var device in devices.Where(d => _deviceMenuItems.All(i => i.Model != d)))
            _deviceMenuItems.Add(new(device.GetDisplayLabel(), device));

        foreach (var slot in _slots)
            slot.ShowSelectedDevice();
    }



    // helpers



    private void SubscribeToKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated += OnSlotCreated;
        list.SlotDeleted += OnSlotDeleted;
        list.DevicesChanged += OnDevicesChanged;
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated -= OnSlotCreated;
        list.SlotDeleted -= OnSlotDeleted;
        list.DevicesChanged -= OnDevicesChanged;
    }



    private void CreateSlot(KeyboardSlotModel slot)
    {
        var viewModel = new KeyboardSlotViewModel(slot, _inputManager, _deviceMenuItems, _keyConfigurator);
        _slots.Add(viewModel);
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromKeyboardListModel(_list);

        foreach (var slot in _slots)
            slot.Dispose();

        _configBar.Dispose();
        _list.Configs.ListChanged -= OnConfigsChanged;
    }
}