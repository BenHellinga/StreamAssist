using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Core.Dragging;

namespace StreamAssist.Components.Input.UI;



// the Keyboard tab's input device boxes, top to bottom. An input device card dropped on a box adds one before it (on
// the drop zone at the end, last); a box dragged by its grip moves the same way
public partial class InputListViewModel : ViewModelBase, IDisposable, IDroppable
{
    // private



    private readonly InputListModel _list;
    private readonly DeviceInputManager _inputManager;
    private readonly KeybindViewModel? _keyConfigurator;

    private readonly DeviceMenus _deviceMenus; // the boxes' dropdown lists
    private readonly ConfigBarViewModel _configBar;
    private ObservableCollection<InputSlotViewModel> _slots;



    // public



    public ObservableCollection<InputSlotViewModel> Slots => _slots;
    public ConfigBarViewModel ConfigBar => _configBar; // one config for the whole tab
    public NoConfigViewModel NoConfig { get; } = new(); // shown instead of the keyboards while there's no config
    public bool HasConfig => _list.Configs.SelectedItem != null;



    // a copied input device box pastes as an InputDeviceCopy (BlockClipboard)
    static InputListViewModel()
    {
        BlockClipboard.RegisterKind(InputDeviceCopy.Kind, data => System.Text.Json.JsonSerializer.Deserialize<InputDeviceCopy>(data));
    }



    // constructor



    public InputListViewModel(InputListModel list, DeviceInputManager inputManager, KeybindViewModel? keyConfigurator = null)
    {
        _list = list;
        _inputManager = inputManager;
        _keyConfigurator = keyConfigurator;

        _deviceMenus = new(_list.Devices ?? []);
        _configBar = new(_list.Configs);
        _slots = [];

        foreach (var slot in _list.Slots)
            CreateSlot(slot, _slots.Count);

        SubscribeToKeyboardListModel(_list);
        _list.Configs.ListChanged += OnConfigsChanged;
    }



    // ui events



    // drag drop



    public bool CanAcceptDropPayload(object payload) =>
        payload is NewInputDevicePayload || payload is InputSlotDragPayload dragged && _list.Slots.Contains(dragged.Slot)
        || payload is InputDeviceCopy && _list.Configs.Selected != null; // a pasted box



    // zoneContext is the box it was dropped on (it goes before that one), or nothing for the zone at the end
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        var index = zoneContext is InputSlotViewModel before ? _slots.IndexOf(before) : _slots.Count;
        if (index < 0) index = _slots.Count;

        if (payload is NewInputDevicePayload card) _list.TryCreateSlot(card.Layout, index, out _);
        else if (payload is InputSlotDragPayload dragged) _list.TryMoveSlot(dragged.Slot, index);
        else if (payload is InputDeviceCopy copy) _list.TryCreateSlotCopy(copy.Layout, copy.IsDisabled, copy.Keys, index);
    }



    // model events



    private void OnSlotCreated(InputSlotModel slot, int index) => CreateSlot(slot, index);



    private void OnSlotMoved(InputSlotModel _, int from, int to) => _slots.Move(from, to);



    private void OnSlotDeleted(InputSlotModel slot)
    {
        var viewModel = _slots.FirstOrDefault(s => s.RepresentsModel(slot));
        if (viewModel is null) return;

        _slots.Remove(viewModel);
        _keyConfigurator?.DeselectKeys(viewModel.Keys);
        viewModel.Dispose();
    }



    private void OnConfigsChanged() => OnPropertyChanged(nameof(HasConfig));



    // a device was plugged in or removed: the dropdowns get the new lists, then each slot shows its device again
    private void OnDevicesChanged()
    {
        _deviceMenus.Update(_list.Devices ?? []);

        foreach (var slot in _slots)
            slot.ShowSelectedDevice();
    }



    // helpers



    private void SubscribeToKeyboardListModel(InputListModel list)
    {
        list.SlotCreated += OnSlotCreated;
        list.SlotDeleted += OnSlotDeleted;
        list.SlotMoved += OnSlotMoved;
        list.DevicesChanged += OnDevicesChanged;
    }



    private void UnsubscribeFromKeyboardListModel(InputListModel list)
    {
        list.SlotCreated -= OnSlotCreated;
        list.SlotDeleted -= OnSlotDeleted;
        list.SlotMoved -= OnSlotMoved;
        list.DevicesChanged -= OnDevicesChanged;
    }



    private void CreateSlot(InputSlotModel slot, int index)
    {
        var viewModel = new InputSlotViewModel(slot, _list, _inputManager, _deviceMenus, _keyConfigurator);
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