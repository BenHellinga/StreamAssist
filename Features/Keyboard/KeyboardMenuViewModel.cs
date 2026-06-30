using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// Drives the KeyboardMenu view (config + device pickers) for ONE keyboard
/// slot. Configs are GLOBAL — shared by name across every slot, backed by
/// KeyboardFeatureSettings — so creating/deleting/renaming a config here
/// affects every slot, kept in sync via _settings.ConfigsChanged. Device
/// selection stays purely local to this slot.
/// </summary>
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

    /// Raised whenever the selected device changes via direct user pick.
    public event Action<string?>? DeviceChanged;

    /// Raised whenever the selected config changes — either a direct user
    /// pick, or an automatic fallback to Default because the previously
    /// selected config was deleted (possibly from a different slot).
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

    /// <summary>
    /// Restores a previously-saved device/config selection without firing
    /// DeviceChanged/ConfigChanged. The caller (KeyboardSlotViewModel) is
    /// responsible for pushing these values into KeyboardDisplay itself,
    /// since those events are suppressed here.
    /// </summary>
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

    private void RefreshConfigNames()
    {
        var latest = _settings.ConfigNames;

        // Remove names that no longer exist
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

    /// <summary>
    /// Fires for EVERY KeyboardMenuViewModel whenever any of them (or any
    /// other config consumer) creates, deletes, or renames a config — that's
    /// what keeps slots' dropdowns in sync with each other.
    /// </summary>
    private void OnConfigsChanged()
    {
        // Capture intent BEFORE touching the collection — a bound ComboBox can
        // null out SelectedConfigName as a side effect of items being
        // added/removed below, so we can't trust the property's value once
        // RefreshConfigNames returns. _settings is the real source of truth.
        var previousSelection = SelectedConfigName;

        RefreshConfigNames();

        var stillExists = previousSelection is not null && _settings.TryGetConfig(previousSelection, out _);

        if (!stillExists)
        {
            // Our config was deleted (by us or another slot) — fall back to
            // Default. Goes through the normal setter, so it naturally cascades:
            // OnSelectedConfigNameChanged → ConfigChanged → this slot's
            // KeyboardDisplay.ApplyConfig("Default").
            SelectedConfigName = "Default";
        }
        else if (SelectedConfigName != previousSelection)
        {
            // Our config is still valid, but the UI binding nulled (or changed)
            // the property as a side effect of the collection mutation —
            // restore it explicitly so this slot's display doesn't go blank.
            SelectedConfigName = previousSelection;
        }
    }

    [RelayCommand]
    private void CreateConfig()
    {
        var name = NewConfigName.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!_settings.CreateConfig(name)) return; // false if name already taken; ConfigsChanged not fired

        // ConfigsChanged has already refreshed ConfigNames (incl. the new
        // one) on every menu by the time CreateConfig returns. Select it
        // here, on this slot specifically.
        SelectedConfigName = name;
        NewConfigName = "";
    }

    [RelayCommand]
    private void DeleteConfig()
    {
        if (SelectedConfigName is not { } name) return;

        // No-op (and no event) if name is "Default" or already gone.
        // Otherwise our own ConfigsChanged subscription — fired
        // synchronously inside this call — handles the refresh + Default
        // fallback for this menu and every other one.
        _settings.DeleteConfig(name);
    }

    public void Dispose()
    {
        _settings.ConfigsChanged -= OnConfigsChanged;
    }
}