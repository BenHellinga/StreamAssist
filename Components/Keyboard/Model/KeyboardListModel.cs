using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardListModel : IJsonOnDeserialized
{
    // constants
    public const string NoneConfig = "None";
    public const string NoneDevice = "None";

    // dependencies
    private IReadOnlyList<KeyboardDeviceModel>? _devices;

    // private storage fields
    private List<KeybindConfigModel> _configs;
    private List<KeyboardSlotModel> _slots;

    // public getter properties
    [JsonIgnore] public IReadOnlyList<KeybindConfigModel> Configs => _configs;
    [JsonIgnore] public IReadOnlyList<KeyboardSlotModel> Slots => _slots;
    [JsonIgnore] public IReadOnlyList<KeyboardDeviceModel>? Devices => _devices;



    // json



    [JsonInclude] [JsonPropertyName("configs")]
    private List<KeybindConfigModel> JsonConfigs
    {
        get => _configs;
        set => _configs = value;
    }

    [JsonInclude] [JsonPropertyName("slots")]
    private List<KeyboardSlotModel> JsonSlots
    {
        get => _slots;
        set => _slots = value;
    }



    // events



    public event Action<KeyboardSlotModel>? SlotAdded;
    public event Action<KeyboardSlotModel>? SlotRemoved;
    public event Action<KeybindConfigModel>? ConfigAdded;
    public event Action<KeybindConfigModel>? ConfigRemoved;
    public event Action? DeviceListUpdated;



    // constructor



    public KeyboardListModel()
    {
        _configs = [];
        _slots = [];
        _devices = null;
    }



    // deserialized callback



    void IJsonOnDeserialized.OnDeserialized()
    {
        foreach (var slot in _slots)
            slot.AttachOwner(this);
    }



    // attach devices



    // TODO should add AddDevice/RemoveDevice methods with device plugged/unplugged callbacks from KeyboardInputManager? 
    public void AttachDevices(IReadOnlyList<KeyboardDeviceModel> devices)
    {
        _devices = devices;
        DeviceListUpdated?.Invoke();

        foreach (var slot in _slots)
            slot.ValidatePendingDevice();
    }



    // slot management



    public KeyboardSlotModel AddSlot()
    {
        var slot = new KeyboardSlotModel(this);
        _slots.Add(slot);
        SlotAdded?.Invoke(slot);
        return slot;
    }



    public bool TryRemoveLastSlot()
    {
        if (_slots.Count <= 0)
        {
            AppLog.WriteError("Could not remove keyboard slot - there are no keyboard slots.");
            return false;
        }

        RemoveLastSlot();
        return true;
    }



    private void RemoveLastSlot()
    {
        var slot = _slots[^1];
        _slots.RemoveAt(_slots.Count - 1);
        SlotRemoved?.Invoke(slot);
    }



    // config management



    public KeybindConfigModel? TryAddConfig(string name, Dictionary<ushort, KeybindModel>? keybinds = null)
    {
        var trimmed = name.Trim();
        
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            AppLog.WriteError("Could not create config - name cannot be empty");
            return null;    
        }

        if (trimmed == NoneConfig)
        {
            AppLog.WriteError($"Could not create config - name cannot be '{NoneConfig}'");
            return null;
        }

        if (_configs.Any(c => c.Name == trimmed))
        {
            AppLog.WriteError("Could not create config - name already in use");
            return null;
        }

        var config = AddConfig(trimmed, keybinds);
        return config;
    }



    private KeybindConfigModel AddConfig(string name, Dictionary<ushort, KeybindModel>? keybinds = null)
    {
        var config = new KeybindConfigModel(name, keybinds);
        _configs.Add(config);
        ConfigAdded?.Invoke(config);
        return config;
    }



    public bool TryRemoveConfig(KeybindConfigModel config)
    {
        if (!_configs.Contains(config))
        {
            AppLog.WriteError("Could not delete config - config does not exist");
            return false;
        }

        RemoveConfig(config);
        return true;
    }



    public bool TryRemoveConfigByString(string configString)
    {
        var config = _configs.FirstOrDefault(c => c.Name == configString);
        if (config is null)
        {
            AppLog.WriteError("Could not delete config - config does not exist");
            return false;
        }

        RemoveConfig(config);
        return true;
    }



    private void RemoveConfig(KeybindConfigModel config)
    {
        foreach (var slot in _slots)
            if (slot.SelectedConfig == config)
                slot.DeselectConfig();

        _configs.Remove(config);
        ConfigRemoved?.Invoke(config);
    }
}