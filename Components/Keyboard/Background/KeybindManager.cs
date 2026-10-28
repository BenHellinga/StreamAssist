using System;
using System.Collections.Generic;
using System.Linq;
using StreamAssist.Components.Keyboard.Model;
using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Components.Keyboard.Background;



// TODO look over and redo (AI generated)
public class KeybindManager : IDisposable
{
    // private



    private readonly KeyboardInputManager _inputManager;
    private readonly KeyboardListModel _listModel;

    private readonly Dictionary<string, List<BindableModel>?[]> _lookup = [];
    private readonly HashSet<(string device, ushort VK)> _keysDown = [];

    private readonly Dictionary<KeyboardSlotModel, SlotTracker> _slotTrackers = [];



    // constructor



    public KeybindManager(KeyboardListModel listModel, KeyboardInputManager inputManager)
    {
        _inputManager = inputManager;
        _listModel = listModel;

        foreach (var slot in _listModel.Slots)
            _slotTrackers[slot] = new SlotTracker(this, slot);

        _listModel.SlotCreated += OnSlotAdded;
        _listModel.SlotDeleted += OnSlotRemoved;

        _inputManager.KeyDown += OnKeyDown;
        _inputManager.KeyUp += OnKeyUp;
    }



    // slot management



    private void OnSlotAdded(KeyboardSlotModel slot) => _slotTrackers[slot] = new SlotTracker(this, slot);



    private void OnSlotRemoved(KeyboardSlotModel slot)
    {
        if (!_slotTrackers.Remove(slot, out var tracker)) return;
        tracker.Dispose();
    }



    // key events



    private void OnKeyDown(string device, ushort vk)
    {
        // windows repeats key down while a key is held, only the first one starts the bind
        var isRepeat = !_keysDown.Add((device, vk));
        if (isRepeat) return;

        var bindings = GetBindings(device, vk);
        if (bindings == null) return;

        foreach (var binding in bindings)
            binding.TriggerStart();
    }



    private void OnKeyUp(string device, ushort vk)
    {
        _keysDown.Remove((device, vk));

        var bindings = GetBindings(device, vk);
        if (bindings == null) return;

        foreach (var binding in bindings)
            binding.TriggerStop();
    }



    private List<BindableModel>? GetBindings(string device, ushort vk)
    {
        if (!_lookup.TryGetValue(device, out var table)) return null;
        return table[vk];
    }



    // table management



    private void Register(string device, ushort vk, BindableModel keybind)
    {
        if (!_lookup.TryGetValue(device, out var table))
        {
            table = new List<BindableModel>?[256];
            _lookup[device] = table;
        }

        (table[vk] ??= []).Add(keybind);
    }



    private void Unregister(string device, ushort vk, BindableModel keybind)
    {
        if (!_lookup.TryGetValue(device, out var table)) return;

        var list = table[vk];
        if (list == null) return;

        list.Remove(keybind);
        if (list.Count == 0) table[vk] = null;
    }



    // disposal



    public void Dispose()
    {
        _listModel.SlotCreated -= OnSlotAdded;
        _listModel.SlotDeleted -= OnSlotRemoved;

        _inputManager.KeyDown -= OnKeyDown;
        _inputManager.KeyUp -= OnKeyUp;

        foreach (var tracker in _slotTrackers.Values)
            tracker.Dispose();

        _slotTrackers.Clear();
    }



    private sealed class SlotTracker : IDisposable
    {
        private readonly KeybindManager _owner;
        private readonly KeyboardSlotModel _slot;

        private string? _device;
        private KeybindConfigModel? _config;

        // all keybinds in config
        private readonly Dictionary<ushort, BindableModel> _bound = [];

        // bound && enabled && has device
        private readonly HashSet<ushort> _registered = [];

        private readonly Dictionary<ushort, Action<bool>> _enabledHandlers = [];



        public SlotTracker(KeybindManager owner, KeyboardSlotModel slot)
        {
            _owner = owner;
            _slot = slot;

            _slot.SelectedDeviceChanged += OnSelectedDeviceChanged;
            _slot.SelectedConfigChanged += OnSelectedConfigChanged;

            _device = _slot.SelectedDevice?.DevicePath;
            BindConfig(_slot.SelectedConfig);
        }



        // model callbacks



        private void OnSelectedDeviceChanged(InputDeviceModel? _, InputDeviceModel? newDevice)
        {
            UnregisterAll();
            _device = newDevice?.DevicePath;
            RegisterAllEnabled();
        }



        private void OnSelectedConfigChanged(KeybindConfigModel? oldConfig, KeybindConfigModel? newConfig)
        {
            UnbindConfig(oldConfig);
            BindConfig(newConfig);
        }



        private void OnKeyBound(ushort vk, BindableModel keybind)
        {
            TrackKey(vk, keybind);
            if (keybind.IsDisabled) return;
            Register(vk, keybind);
        }



        private void OnKeyUnbound(ushort vk, BindableModel _)
        {
            Unregister(vk);
            UntrackKey(vk);
        }



        // config subscription helpers



        private void BindConfig(KeybindConfigModel? config)
        {
            _config = config;
            if (_config == null) return;

            _config.KeyBound += OnKeyBound;
            _config.KeyUnbound += OnKeyUnbound;

            foreach (var (vk, keybind) in _config.Keybinds)
                TrackKey(vk, keybind);

            RegisterAllEnabled();
        }



        private void UnbindConfig(KeybindConfigModel? config)
        {
            UnregisterAll();

            foreach (var vk in _bound.Keys.ToArray())
                UntrackKey(vk);

            if (config == null) return;
            config.KeyBound -= OnKeyBound;
            config.KeyUnbound -= OnKeyUnbound;
        }



        // per-key tracking (bound, independent of device/enabled state)



        private void TrackKey(ushort vk, BindableModel keybind)
        {
            _bound[vk] = keybind;

            void OnIsDisabledChanged(bool isDisabled)
            {
                if (isDisabled) Unregister(vk);
                else Register(vk, keybind);
            }

            _enabledHandlers[vk] = OnIsDisabledChanged;
            keybind.IsDisabledChanged += OnIsDisabledChanged;
        }



        private void UntrackKey(ushort vk)
        {
            if (!_bound.TryGetValue(vk, out var keybind)) return;

            if (_enabledHandlers.Remove(vk, out var handler))
                keybind.IsDisabledChanged -= handler;

            _bound.Remove(vk);
        }



        // dispatch table registration (bound AND enabled AND has a device)



        private void RegisterAllEnabled()
        {
            if (_device == null) return;
            foreach (var (vk, keybind) in _bound)
                if (!keybind.IsDisabled)
                    Register(vk, keybind);
        }



        private void UnregisterAll()
        {
            foreach (var vk in _registered.ToArray())
                Unregister(vk);
        }



        private void Register(ushort vk, BindableModel keybind)
        {
            if (_device == null) return;
            if (!_registered.Add(vk)) return;
            _owner.Register(_device, vk, keybind);
        }



        private void Unregister(ushort vk)
        {
            if (_device == null) return;
            if (!_registered.Remove(vk)) return;
            if (_bound.TryGetValue(vk, out var keybind))
                _owner.Unregister(_device, vk, keybind);
        }



        // disposal



        public void Dispose()
        {
            _slot.SelectedDeviceChanged -= OnSelectedDeviceChanged;
            _slot.SelectedConfigChanged -= OnSelectedConfigChanged;

            UnbindConfig(_config);
        }
    }
}