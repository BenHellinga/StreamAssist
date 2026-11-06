using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Keyboard.Background;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Keyboard.UI;



// the Keyboard tab's input device boxes, top to bottom. An input device card dropped on a box adds one before it (on
// the drop zone at the end, last); a box dragged by its grip moves the same way
public partial class KeyboardListViewModel : ViewModelBase, IDisposable, IDroppable
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
            CreateSlot(slot, _slots.Count);

        SubscribeToKeyboardListModel(_list);
        _list.Configs.ListChanged += OnConfigsChanged;
    }



    // ui events



    // drag drop



    public bool CanAcceptDropPayload(object payload) =>
        payload is NewInputDevicePayload || payload is KeyboardSlotDragPayload dragged && _list.Slots.Contains(dragged.Slot);



    // zoneContext is the box it was dropped on (it goes before that one), or nothing for the zone at the end
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        var index = zoneContext is KeyboardSlotViewModel before ? _slots.IndexOf(before) : _slots.Count;
        if (index < 0) index = _slots.Count;

        if (payload is NewInputDevicePayload card) _list.TryCreateSlot(card.Layout, index, out _);
        else if (payload is KeyboardSlotDragPayload dragged) _list.TryMoveSlot(dragged.Slot, index);
    }



    // model events



    private void OnSlotCreated(KeyboardSlotModel slot, int index) => CreateSlot(slot, index);



    private void OnSlotMoved(KeyboardSlotModel _, int from, int to) => _slots.Move(from, to);



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
        list.SlotMoved += OnSlotMoved;
        list.DevicesChanged += OnDevicesChanged;
    }



    private void UnsubscribeFromKeyboardListModel(KeyboardListModel list)
    {
        list.SlotCreated -= OnSlotCreated;
        list.SlotDeleted -= OnSlotDeleted;
        list.SlotMoved -= OnSlotMoved;
        list.DevicesChanged -= OnDevicesChanged;
    }



    private void CreateSlot(KeyboardSlotModel slot, int index)
    {
        var viewModel = new KeyboardSlotViewModel(slot, _list, _inputManager, _deviceMenuItems, _keyConfigurator);
        _slots.Insert(index, viewModel);
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