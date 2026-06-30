using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the KeyboardMenu view (config + device pickers) for ONE keyboard
/// slot. Configs are scoped entirely to this slot — two different slots can
/// both have a config named "Streaming" and they share nothing.
///
/// NOTE: configs are in-memory only for now, not yet written through to
/// KeyboardFeatureSettings/disk — that lands with the rest of the
/// persistence rework next round. Right now a "config" is just a saved
/// device selection; once key-binding persistence exists it'll live here
/// too, and CreateConfig's "copy" will pick that up automatically.
/// </summary>
public partial class KeyboardMenuViewModel : ViewModelBase
{
    private const string DefaultConfigName = "Default";

    public ObservableCollection<KeyboardInfo> AvailableDevices { get; } = [];
    public ObservableCollection<string> ConfigNames { get; } = [];
    public static readonly KeyboardInfo NoneDevice = new(null, "None", "", 0, 0);

    [ObservableProperty] private KeyboardInfo? _selectedDevice;
    [ObservableProperty] private string? _selectedConfigName;
    [ObservableProperty] private string _newConfigName = "";

    // name -> selected device id, per config, scoped to this slot only.
    private readonly Dictionary<string, string?> _configDeviceIds = new() { [DefaultConfigName] = null };

    private bool _suppressCallbacks;

    /// Raised whenever the effective device for this slot changes — either
    /// from picking a device directly, or from switching configs. The
    /// owning KeyboardSlotViewModel forwards this to its KeyboardDisplay.
    public event Action<string?>? DeviceChanged;



    public KeyboardMenuViewModel()
    {
        RefreshDevices();
        ConfigNames.Add(DefaultConfigName);

        _suppressCallbacks = true;
        SelectedConfigName = DefaultConfigName;
        SelectedDevice = NoneDevice;
        _suppressCallbacks = false;
    }



    // helper methods



    private void RefreshDevices()
    {
        AvailableDevices.Clear();
        AvailableDevices.Add(NoneDevice);
        foreach (var d in InputManager.GetAvailableKeyboards())
            AvailableDevices.Add(d);
    }



    private KeyboardInfo FindDevice(string? devicePath)
    {
        if (devicePath is null) return NoneDevice;

        foreach (var d in AvailableDevices)
            if (d.DevicePath == devicePath) return d;

        return NoneDevice;
    }



    // callbacks



    partial void OnSelectedDeviceChanged(KeyboardInfo? value)
    {
        if (_suppressCallbacks) return;
        if (value is null) return;

        if (SelectedConfigName is { } name)
            _configDeviceIds[name] = value.DevicePath;

        DeviceChanged?.Invoke(value.DevicePath);
    }



    partial void OnSelectedConfigNameChanged(string? value)
    {
        if (_suppressCallbacks) return;
        if (value is null) return;
        if (!_configDeviceIds.TryGetValue(value, out var deviceId)) return;

        _suppressCallbacks = true;
        SelectedDevice = FindDevice(deviceId);
        _suppressCallbacks = false;

        DeviceChanged?.Invoke(deviceId);
    }



    [RelayCommand]
    private void CreateConfig()
    {
        var name = NewConfigName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (_configDeviceIds.ContainsKey(name)) return;

        // Copy this slot's currently-selected device into the new config.
        _configDeviceIds[name] = SelectedDevice?.DevicePath;
        ConfigNames.Add(name);
        SelectedConfigName = name;
        NewConfigName = "";
    }



    [RelayCommand]
    private void DeleteConfig()
    {
        if (SelectedConfigName is not { } name) return;
        if (name == DefaultConfigName) return;

        _configDeviceIds.Remove(name);
        ConfigNames.Remove(name);
        SelectedConfigName = DefaultConfigName;
    }
}