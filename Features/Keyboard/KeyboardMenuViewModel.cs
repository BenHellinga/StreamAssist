using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class KeyboardMenuViewModel : ViewModelBase, IDisposable
{
    private readonly KeyboardFeatureSettings _settings;

    public ObservableCollection<KeyboardInfo> AvailableDevices { get; } = [];
    public ObservableCollection<string> ConfigNames { get; } = [];
    public static readonly KeyboardInfo NoneDevice = new(null, "None", "", 0, 0);

    [ObservableProperty] private KeyboardInfo? _selectedDevice;
    [ObservableProperty] private string? _selectedConfigName;
    [ObservableProperty] private string _newConfigName = "";

    private bool _suppressCallbacks;



    public event Action<string?>? DeviceChanged;
    public event Action<string>? ConfigChanged;



    public KeyboardMenuViewModel(KeyboardFeatureSettings settings)
    {
        _settings = settings;
        _settings.ConfigsChanged += OnConfigsChanged;

        RefreshDevices();
        RefreshConfigNames();

        _suppressCallbacks = true;
        SelectedConfigName = ConfigNames.Contains("Default") ? "Default" : ConfigNames.FirstOrDefault() ?? "Default";
        SelectedDevice = NoneDevice;
        _suppressCallbacks = false;
    }



    // ???
    public void InitializeFrom(KeyboardSlotData data)
    {
        _suppressCallbacks = true;
        SelectedConfigName = ConfigNames.Contains(data.SelectedConfigName) ? data.SelectedConfigName : "Default";
        SelectedDevice = FindDevice(data.SelectedDeviceId);
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



    // ???
    private void RefreshConfigNames()
    {
        var latest = _settings.ConfigNames;

        for (int i = ConfigNames.Count - 1; i >= 0; i--)
            if (!latest.Contains(ConfigNames[i]))
                ConfigNames.RemoveAt(i);

        // Add any new names that aren't already present
        foreach (var name in latest)
            if (!ConfigNames.Contains(name))
                ConfigNames.Add(name);
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

        DeviceChanged?.Invoke(value.DevicePath);
    }



    partial void OnSelectedConfigNameChanged(string? value)
    {
        if (_suppressCallbacks) return;
        if (value is null) return;

        ConfigChanged?.Invoke(value);
    }



    private void OnConfigsChanged()
    {
        var previousSelection = SelectedConfigName;

        RefreshConfigNames();

        var stillExists = previousSelection is not null && _settings.TryGetConfig(previousSelection, out _);

        if (!stillExists)
            SelectedConfigName = "Default";
        else if (SelectedConfigName != previousSelection)
            SelectedConfigName = previousSelection;
    }



    [RelayCommand]
    private void CreateConfig()
    {
        var name = NewConfigName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!_settings.CreateConfig(name)) return; // false if name already taken; ConfigsChanged not fired

        SelectedConfigName = name;
        NewConfigName = "";
    }



    [RelayCommand]
    private void DeleteConfig()
    {
        if (SelectedConfigName is not { } name) return;

        _settings.DeleteConfig(name);
    }



    public void Dispose()
    {
        _settings.ConfigsChanged -= OnConfigsChanged;
    }
}