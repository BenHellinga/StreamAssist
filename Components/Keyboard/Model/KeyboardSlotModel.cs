using System;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardSlotModel
{
    // private


    private KeyboardDeviceModel? _selectedDevice;
    private KeyboardConfigModel? _selectedConfig;
    private KeyboardListModel? _ownerList;
    
    private string? _pendingDeviceString;
    private string? _pendingConfigString;



    [JsonInclude]
    [JsonPropertyName("selectedDevice")]
    private string? PendingDevice
    {
        get => _selectedDevice?.DevicePath;
        set => _pendingDeviceString = value;
    }

    [JsonInclude]
    [JsonPropertyName("selectedConfig")]
    private string? PendingConfig
    {
        get => _selectedConfig?.Name;
        set => _pendingConfigString = value;
    }



    // public



    public int SlotID => _ownerList?.Slots.IndexOf(this) ?? -1;

    [JsonIgnore]
    public KeyboardDeviceModel? SelectedDevice
    {
        get => _selectedDevice;
        set => TrySetSelectedDevice(value);
    }

    [JsonIgnore]
    public KeyboardConfigModel? SelectedConfig
    {
        get => _selectedConfig;
        set => TrySetSelectedConfig(value);
    }



    // events



    public event Action<KeyboardDeviceModel?>? SelectedDeviceChanged;
    public event Action<KeyboardConfigModel?>? SelectedConfigChanged;



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



    // post deserialize methods



    public void AttachOwner(KeyboardListModel ownerList)
    {
        _ownerList = ownerList;

        var device = _ownerList.Devices.FirstOrDefault(d => d.DevicePath == _pendingDeviceString);
        if (device == null && _pendingDeviceString != null)
        {
            AppLog.WriteWarning($"The device '{_pendingDeviceString}' could not be found and therefore was deselected on keyboard slot {SlotID}");
            TrySetSelectedDevice(null);
        }
        else SetSelectedDevice(device);

        var config = _ownerList.Configs.FirstOrDefault(c => c.Name == _pendingConfigString);
        if (config == null && _pendingConfigString != null)
        {
            AppLog.WriteWarning($"The config '{_pendingConfigString}' could not be found and therefore was deselected on keyboard slot {SlotID}");
            TrySetSelectedConfig(null);
        }
        else SetSelectedConfig(config);
    }



    // setters



    public bool TrySetSelectedDevice(KeyboardDeviceModel? device)
    {
        if (device != null)
        {
            if (_ownerList == null)
            {
                AppLog.WriteError($"Could not select device '{device.DevicePath}' - no ownerList to confirm validity");
                return false;
            }

            if (!_ownerList.Devices.Contains(device))
            {
                AppLog.WriteError($"Could not select device '{device.DevicePath}' - device not found in list");
                return false;
            }
        }

        SetSelectedDevice(device);
        return true;
    }



    public bool TrySetSelectedDeviceByString(string? deviceString)
    {
        KeyboardDeviceModel? device = null;

        if (deviceString != null)
        {
            if (_ownerList == null)
            {
                AppLog.WriteError($"Could not select device '{deviceString}' - no ownerList to confirm validity");
                return false;
            }

            device = _ownerList.Devices.FirstOrDefault(d => d.DevicePath == deviceString);
            if (device == null)
            {
                AppLog.WriteError($"Could not select device '{deviceString}' - device not found in list");
                return false;
            }
        }

        SetSelectedDevice(device);
        return true;
    }



    public void SetSelectedDevice(KeyboardDeviceModel? device)
    {
        if (_selectedDevice == device) return;

        _selectedDevice = device;
        SelectedDeviceChanged?.Invoke(device);
    }



    public bool TrySetSelectedConfig(KeyboardConfigModel? config)
    {
        if (config != null)
        {
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
        }
    
        SetSelectedConfig(config);
        return true;
    }



    public bool TrySetSelectedConfigByString(string? configString)
    {
        KeyboardConfigModel? config = null;

        if (configString != null)
        {
            if (_ownerList == null)
            {
                AppLog.WriteError($"Could not select config '{configString}' - no ownerList to confirm validity");
                return false;
            }

            config = _ownerList.Configs.FirstOrDefault(c => c.Name == configString);
            if (config == null)
            {
                AppLog.WriteError($"Could not select config '{configString}' - config not found in list");
                return false;
            }
        }

        SetSelectedConfig(config);
        return true;
    }



    public void SetSelectedConfig(KeyboardConfigModel? config)
    {
        if (_selectedConfig == config) return;
        _selectedConfig = config;
        SelectedConfigChanged?.Invoke(config);
    }
}