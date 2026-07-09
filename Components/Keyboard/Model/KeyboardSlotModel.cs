using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.Keyboard.Model;



public class KeyboardSlotModel
{
    private string _selectedDevice = KeyboardListModel.NoneDevice;
    private string _selectedConfig = KeyboardListModel.NoneConfig;



    [JsonPropertyName("selectedDevice")]
    public string SelectedDevice
    {
        get => _selectedDevice;
        set => SetDevice(value);
    }

    [JsonPropertyName("selectedConfig")]
    public string SelectedConfig
    {
        get => _selectedConfig;
        set => SetConfig(value);
    }



    // events



    public event Action<string>? SelectedDeviceChanged;
    public event Action<string>? ConfigChanged;



    // setters



    public void SetDevice(string? device)
    {
        var value = string.IsNullOrEmpty(device) ? KeyboardListModel.NoneDevice : device;
        if (_selectedDevice == value) return;
        // TODO check existance of device here (move device list to here or KeyboardListModel)

        _selectedDevice = value;
        SelectedDeviceChanged?.Invoke(_selectedDevice);
    }



    public void SetConfig(string? config)
    {
        var value = string.IsNullOrEmpty(config) ? KeyboardListModel.NoneConfig : config;
        // TODO check existance of config here
        if (_selectedConfig == value) return;

        _selectedConfig = value;
        ConfigChanged?.Invoke(_selectedConfig);
    }
}