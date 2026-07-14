using System;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardSlotModel
{
    // dependencies
    private KeyboardListModel? _ownerList;

    // private storage fields
    private KeyboardDeviceModel? _selectedDevice;
    private KeybindConfigModel? _selectedConfig;
    
    // public getter properties
    [JsonIgnore] public KeyboardDeviceModel? SelectedDevice => _selectedDevice;
    [JsonIgnore] public KeybindConfigModel? SelectedConfig => _selectedConfig;
    [JsonIgnore] public int SlotID => _ownerList?.Slots.IndexOf(this) ?? -1;



    // json



    private string? _pendingDeviceString;
    private string? _pendingConfigString;



    [JsonInclude] [JsonPropertyName("selectedDevice")]
    private string? JsonSelectedDevice
    {
        get => _selectedDevice?.DevicePath;
        set => _pendingDeviceString = value;
    }

    [JsonInclude] [JsonPropertyName("selectedConfig")]
    private string? JsonSelectedConfig
    {
        get => _selectedConfig?.Name;
        set => _pendingConfigString = value;
    }



    // events



    public event Action<KeyboardDeviceModel?, KeyboardDeviceModel?>? SelectedDeviceChanged;
    public event Action<KeybindConfigModel?, KeybindConfigModel?>? SelectedConfigChanged;



    // constructors



    public KeyboardSlotModel()
    {
        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _pendingConfigString = null;

        _ownerList = null;
    }



    public KeyboardSlotModel(KeyboardListModel ownerList)
    {
        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _pendingConfigString = null;

        _ownerList = ownerList;
    }



    // attach owner



    public void AttachOwner(KeyboardListModel ownerList)
    {
        _ownerList = ownerList;
        ValidatePendingConfig();
    }



    // validation methods



    public void ValidatePendingConfig()
    {
        if (_pendingConfigString == null) return;
        if (_ownerList == null) return;
        
        var config = _ownerList.Configs.FirstOrDefault(c => c.Name == _pendingConfigString);
        if (config == null)
        {
            AppLog.WriteWarning($"The config '{_pendingConfigString}' on keyboard slot {SlotID} could not be found and was deselected");
            SetSelectedConfig(null);
        }
        else SetSelectedConfig(config);

        _pendingConfigString = null;
    }



    public void ValidatePendingDevice()
    {
        if (_pendingDeviceString == null) return;
        if (_ownerList == null) return;
        if (_ownerList.Devices == null) return;

        var device = _ownerList.Devices.FirstOrDefault(d => d.DevicePath == _pendingDeviceString);
        if (device == null)
        {
            AppLog.WriteWarning($"The device '{_pendingDeviceString}' on keyboard slot {SlotID} could not be found and was deselected");
            TrySetSelectedDevice(null);
        }
        else SetSelectedDevice(device);

        _pendingDeviceString = null;
    }



    // device management



    public bool TrySetSelectedDevice(KeyboardDeviceModel? device)
    {
        if (device == null)
        {
            SetSelectedDevice(device);
            return true;
        }

        if (_ownerList == null)
        {
            AppLog.WriteError($"Could not select device '{device.DevicePath}' - no owner to confirm validity");
            return false;
        }

        if (_ownerList.Devices == null)
        {
            AppLog.WriteError($"Could not select device '{device.DevicePath}' - no device list to confirm validity");
            return false;
        }

        if (!_ownerList.Devices.Contains(device))
        {
            AppLog.WriteError($"Could not select device '{device.DevicePath}' - device not found in list");
            return false;
        }

        SetSelectedDevice(device);
        return true;
    }



    public KeyboardDeviceModel? TrySetSelectedDeviceByString(string? deviceString)
    {
        if (deviceString == null)
        {
            SetSelectedDevice(null);
            return null;
        }

        if (_ownerList == null)
        {
            AppLog.WriteError($"Could not select device '{deviceString}' by string - no ownerList to confirm validity");
            return null;
        }

        if (_ownerList.Devices == null)
        {
            AppLog.WriteError($"Could not select device '{deviceString}' by string - no KeyboardDeviceModel list to confirm validity");
            return null;
        }

        var device = _ownerList.Devices.FirstOrDefault(d => d.DevicePath == deviceString);
        if (device == null)
        {
            AppLog.WriteError($"Could not select device '{deviceString}' by string - device not found in list");
            return null;
        }

        SetSelectedDevice(device);
        return device;
    }



    private void SetSelectedDevice(KeyboardDeviceModel? newDevice)
    {
        if (_selectedDevice == newDevice) return;

        var oldDevice = _selectedDevice;
        _selectedDevice = newDevice;
        SelectedDeviceChanged?.Invoke(oldDevice, newDevice);
    }



    public void DeselectDevice()
    {
        var oldDevice = _selectedDevice;
        _selectedDevice = null;
        SelectedDeviceChanged?.Invoke(oldDevice, null);
    }



    // config management



    public bool TrySetSelectedConfig(KeybindConfigModel? config)
    {
        if (config == null)
        {
            SetSelectedConfig(config);
            return true;
        }

        if (_ownerList == null)
        {
            AppLog.WriteError($"Could not select config '{config.Name}' - no ownerList to confirm validity");
            return false;
        }
        
        if (!_ownerList.Configs.Contains(config))
        {
            AppLog.WriteError($"Could not select config '{config.Name}' - config not found in list");
            return false;
        }
    
        SetSelectedConfig(config);
        return true;
    }



    public KeybindConfigModel? TrySetSelectedConfigByString(string? configString)
    {
        if (configString == null)
        {
            SetSelectedConfig(null);
            return null;
        }

        if (_ownerList == null)
        {
            AppLog.WriteError($"Could not select config '{configString}' - no ownerList to confirm validity");
            return null;
        }

        var config = _ownerList.Configs.FirstOrDefault(c => c.Name == configString);
        if (config == null)
        {
            AppLog.WriteError($"Could not select config '{configString}' - config not found in list");
            return null;
        }

        SetSelectedConfig(config);
        return config;
    }



    private void SetSelectedConfig(KeybindConfigModel? newConfig)
    {
        if (_selectedConfig == newConfig) return;

        var oldConfig = _selectedConfig;
        _selectedConfig = newConfig;
        SelectedConfigChanged?.Invoke(oldConfig, newConfig);
    }



    public void DeselectConfig()
    {
        var oldConfig = _selectedConfig;
        _selectedConfig = null;
        SelectedConfigChanged?.Invoke(oldConfig, null);
    }
}