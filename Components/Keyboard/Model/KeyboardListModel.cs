using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using StreamAssist.Components.Main;
using StreamAssist.Components.StreamAction;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardListModel : IJsonOnDeserialized
{
    public const string NoneConfig = "None";
    public const string NoneDevice = "None";

    [JsonPropertyName("configs")]
    public List<KeyboardConfigModel> Configs { get; set; }

    [JsonPropertyName("slots")]
    public List<KeyboardSlotModel> Slots { get; set; }

    [JsonIgnore]
    public IReadOnlyList<KeyboardDeviceModel> Devices { get; set; }



    // events



    public event Action<KeyboardSlotModel>? SlotAdded;
    public event Action<KeyboardSlotModel>? SlotRemoved;
    public event Action? ConfigListUpdated;
    public event Action? DeviceListUpdated;
    public event Action<string>? ConfigBindingChanged;



    // constructor



    public KeyboardListModel()
    {
        Configs = [];
        Slots = [];
        Devices = [];
    }



    // deserialized callback



    void IJsonOnDeserialized.OnDeserialized()
    {
        foreach (var slot in Slots)
            slot.AttachOwner(this);
    }



    // devices list



    public void SetDevices(IReadOnlyList<KeyboardDeviceModel> devices)
    {
        Devices = devices;
        DeviceListUpdated?.Invoke();
    }



    // slot management



    public void AddSlot()
    {
        var slot = new KeyboardSlotModel(this);
        Slots.Add(slot);
        SlotAdded?.Invoke(slot);
    }



    public bool TryRemoveLastSlot()
    {
        if (Slots.Count <= 0)
        {
            AppLog.WriteError("Could not remove keyboard slot - there are no keyboard slots.");
            return false;
        }

        var slot = Slots[^1];
        Slots.RemoveAt(Slots.Count - 1);
        SlotRemoved?.Invoke(slot);

        return true;
    }



    // config management



    public KeyboardConfigModel? TryCreateConfigByString(string name, Dictionary<ushort, string>? bindings = null)
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

        if (Configs.Any(c => c.Name == trimmed))
        {
            AppLog.WriteError("Could not create config - name already in use");
            return null;
        }

        var config = new KeyboardConfigModel(trimmed, bindings);

        CreateConfig(config);
        return config;
    }



    public void CreateConfig(KeyboardConfigModel config)
    {
        Configs.Add(config);
        ConfigListUpdated?.Invoke();
    }



    public bool TryDeleteConfig(KeyboardConfigModel config)
    {
        if (!Configs.Contains(config))
        {
            AppLog.WriteError("Could not delete config - config does not exist");
            return false;
        }

        DeleteConfig(config);
        return true;
    }



    public bool TryDeleteConfigByString(string configString)
    {
        var config = Configs.FirstOrDefault(c => c.Name == configString);
        if (config is null)
        {
            AppLog.WriteError("Could not delete config - config does not exist");
            return false;
        }

        DeleteConfig(config);
        return true;
    }



    public void DeleteConfig(KeyboardConfigModel config)
    {
        foreach (var slot in Slots)
            if (slot.SelectedConfig == config)
                slot.SetSelectedConfig(null);

        Configs.Remove(config);
        ConfigListUpdated?.Invoke();
    }



    // TODO MOVE THESE TO KeyboardConfigModel




    public bool ChangeConfigBinding(string configName, ushort vk, string? actionTypeName)
    {
        var config = Configs.FirstOrDefault(c => c.Name == configName);
        if (config is null)
        {
            AppLog.WriteError("Could not change config binding - config does not exist");
            return false;
        }

        if (actionTypeName is null) config.Keybindings.Remove(vk);
        else config.Keybindings[vk] = actionTypeName;

        ConfigBindingChanged?.Invoke(configName);

        return true;
    }



    public Dictionary<ushort, IStreamAction> GetConfigBindings(string configName)
    {
        var result = new Dictionary<ushort, IStreamAction>();
        var config = Configs.FirstOrDefault(c => c.Name == configName);
        if (config is null) return result;

        foreach (var (vk, typeName) in config.Keybindings)
        {
            var action = ActionResolver.Resolve(typeName);
            if (action is not null)
                result[vk] = action;
        }

        return result;
    }
}