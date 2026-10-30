using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains the data for a single keyboard slot
public class KeyboardSlotModel : ModelBase
{
    // private



    private IReadOnlyList<InputDeviceModel>? _devices;
    private IReadOnlyList<KeybindConfigModel>? _configs;

    private InputDeviceModel? _selectedDevice;
    private KeybindConfigModel? _selectedConfig;
    private string? _pendingDeviceString;
    private string? _pendingConfigString;


    
    // public



    [JsonIgnore] public InputDeviceModel? SelectedDevice => _selectedDevice;
    [JsonIgnore] public KeybindConfigModel? SelectedConfig => _selectedConfig;



    // json



    [JsonInclude] [JsonPropertyName("selectedDevice")]
    private string? JsonSelectedDevice
    {
        get => _selectedDevice?.DevicePath ?? _pendingDeviceString;
        set => _pendingDeviceString = value; // defer validation until device list is attached
    }

    [JsonInclude] [JsonPropertyName("selectedConfig")]
    private string? JsonSelectedConfig
    {
        get => _selectedConfig?.Name ?? _pendingConfigString;
        set => _pendingConfigString = value; // defer validation until config list is attached
    }



    // events



    public event Action<InputDeviceModel?, InputDeviceModel?>? SelectedDeviceChanged;
    public event Action<KeybindConfigModel?, KeybindConfigModel?>? SelectedConfigChanged;



    // constructors



    public KeyboardSlotModel()
    {
        _devices = null;
        _configs = null;

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _pendingConfigString = null;
    }



    public KeyboardSlotModel(IReadOnlyList<InputDeviceModel>? devices, IReadOnlyList<KeybindConfigModel>? configs)
    {
        _devices = devices;
        _configs = configs;

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _pendingConfigString = null;
    }



    // attach methods



    public void AttachConfigs(IReadOnlyList<KeybindConfigModel> configs)
    {
        _configs = configs;
        if (_pendingConfigString == null) return;
        
        var config = _configs.FirstOrDefault(c => c.Name == _pendingConfigString);
        if (config == null)
        {
            AppLog.WriteWarning($"config '{_pendingConfigString}' not found");
            SetSelectedConfig(null);
            return; // pendingConfigString maintains its value until config is explicitly set
        }

        _pendingConfigString = null;
        SetSelectedConfig(config);
    }



    public void AttachDevices(IReadOnlyList<InputDeviceModel> devices)
    {
        _devices = devices;
        if (_pendingDeviceString == null) return;

        var device = _devices.FirstOrDefault(d => d.DevicePath == _pendingDeviceString);
        if (device == null)
        {
            AppLog.WriteWarning($"keyboard '{_pendingDeviceString}' not found");
            TrySetSelectedDevice(null);
            return; // pendingDeviceString maintains its value until device is explicitly set
        }
        
        _pendingDeviceString = null;
        SetSelectedDevice(device);
    }



    // device management



    public bool TrySetSelectedDevice(InputDeviceModel? device)
    {
        if (device == null)
        {
            SetSelectedDevice(device);
            return true;
        }

        if (!_devices?.Contains(device) ?? false)
        {
            AppLog.WriteError($"keyboard '{device.DevicePath}' not found");
            return false;
        }

        SetSelectedDevice(device);
        return true;
    }



    private void SetSelectedDevice(InputDeviceModel? newDevice)
    {
        if (_selectedDevice == newDevice) return;

        var oldDevice = _selectedDevice;
        _selectedDevice = newDevice;
        SelectedDeviceChanged?.Invoke(oldDevice, _selectedDevice);
    }



    // config management



    public bool TrySetSelectedConfig(KeybindConfigModel? config)
    {
        if (config == null)
        {
            SetSelectedConfig(config);
            return true;
        }
        
        if (!_configs?.Contains(config) ?? false)
        {
            AppLog.WriteError($"config '{config.Name}' not found");
            return false;
        }
    
        SetSelectedConfig(config);
        return true;
    }



    private void SetSelectedConfig(KeybindConfigModel? newConfig)
    {
        if (_selectedConfig == newConfig) return;

        var oldConfig = _selectedConfig;
        _selectedConfig = newConfig;
        SelectedConfigChanged?.Invoke(oldConfig, _selectedConfig);
    }



    // disposal



    public override void Dispose()
    {
        _selectedDevice?.Dispose();
        _selectedConfig?.Dispose();
    }
}