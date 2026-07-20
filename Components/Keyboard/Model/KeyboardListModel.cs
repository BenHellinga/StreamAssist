using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains data for the list of keybind configs and keyboard slots
public class KeyboardListModel : ModelBase, IJsonOnDeserialized
{
    // constants



    public const string NoneConfig = "None"; // reserved for ui
    public const string NoneDevice = "None"; // reserved for ui



    // private



    private IReadOnlyList<InputDeviceModel>? _devices;
    private List<KeybindConfigModel> _configs;
    private List<KeyboardSlotModel> _slots;



    // public



    [JsonIgnore] public IReadOnlyList<KeybindConfigModel> Configs => _configs;
    [JsonIgnore] public IReadOnlyList<KeyboardSlotModel> Slots => _slots;
    [JsonIgnore] public IReadOnlyList<InputDeviceModel>? Devices => _devices;



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



    public event Action<KeyboardSlotModel>? SlotCreated;
    public event Action<KeyboardSlotModel>? SlotDeleted;
    public event Action<KeybindConfigModel>? ConfigCreated;
    public event Action<KeybindConfigModel>? ConfigDeleted;



    // constructor



    public KeyboardListModel()
    {
        _configs = [];
        _slots = [];
        _devices = null; // set later through AttachDevices
    }



    // deserialized



    // json deserialization is done in reverse, so this has to be done after all slots are already made
    void IJsonOnDeserialized.OnDeserialized()
    {
        foreach (var slot in _slots)
            slot.AttachConfigs(_configs);
    }



    // attach devices



    // TODO should add AddDevice/RemoveDevice methods with device plugged/unplugged callbacks from KeyboardInputManager?
    // devices are obtained later during runtime, so attachment happens after list and slots are already made
    public void AttachDevices(IReadOnlyList<InputDeviceModel> devices)
    {
        _devices = devices;

        foreach (var slot in _slots)
            slot.AttachDevices(_devices);
    }



    // slot management



    public void CreateSlot(out KeyboardSlotModel slot)
    {
        slot = new KeyboardSlotModel(_devices, _configs);
        _slots.Add(slot);
        SlotCreated?.Invoke(slot);
    }



    public bool TryDeleteSlot(KeyboardSlotModel slot)
    {
        if (!_slots.Contains(slot))
        {
            AppLog.WriteError("Could not delete keyboard slot - slot not found in list");
            return false;
        }

        DeleteSlot(slot);
        return true;
    }



    private void DeleteSlot(KeyboardSlotModel slot)
    {
        _slots.Remove(slot);
        SlotDeleted?.Invoke(slot);
        slot.Dispose();
    }



    // config management



    public bool TryCreateConfig(string name, Dictionary<ushort, KeybindModel>? keybinds, out KeybindConfigModel config)
    {
        config = null!;
        var trimmed = name.Trim();
        
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            AppLog.WriteError("Could not create config - name cannot be empty");
            return false;    
        }

        if (trimmed == NoneConfig)
        {
            AppLog.WriteError($"Could not create config - name cannot be '{NoneConfig}'");
            return false;
        }

        if (_configs.Any(c => c.Name == trimmed))
        {
            AppLog.WriteError($"Could not create config - '{trimmed}' already in use");
            return false;
        }

        CreateConfig(trimmed, keybinds, out config);
        return true;
    }



    private void CreateConfig(string name, Dictionary<ushort, KeybindModel>? keybinds, out KeybindConfigModel config)
    {
        config = new KeybindConfigModel(name, keybinds);
        _configs.Add(config);
        ConfigCreated?.Invoke(config);
    }



    public bool TryDeleteConfig(KeybindConfigModel config)
    {
        if (!_configs.Contains(config))
        {
            AppLog.WriteError("Could not delete config - config not found in list");
            return false;
        }

        DeleteConfig(config);
        return true;
    }



    private void DeleteConfig(KeybindConfigModel config)
    {
        foreach (var slot in _slots)
            if (slot.SelectedConfig == config)
                slot.TrySetSelectedConfig(null);

        _configs.Remove(config);
        ConfigDeleted?.Invoke(config);
        config.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        foreach (var slot in _slots)
            slot.Dispose();

        foreach (var config in _configs)
            config.Dispose();
    }
}