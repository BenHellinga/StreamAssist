using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Features.Shell;
using System.Diagnostics;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the TopMenu view (config + device pickers).
/// </summary>
public partial class TopMenuViewModel : ViewModelBase
{
    // general object classes
    private readonly InputManager _inputManager;
    private readonly KeyboardFeatureSettings _settings;

    // menu items
    public ObservableCollection<KeyboardInfo> AvailableDevices { get; } = [];
    public ObservableCollection<string> ConfigNames { get; } = [];
    public static readonly KeyboardInfo NoneDevice = new(null, "None", "", 0, 0);
    [ObservableProperty] private KeyboardInfo? _selectedDevice;
    [ObservableProperty] private string? _selectedConfigName;
    [ObservableProperty] private string _newConfigName = "";

    // callback control
    private bool _suppressCallbacks;



    // constructor



    public TopMenuViewModel(InputManager inputManager, KeyboardFeatureSettings settings)
    {
        _inputManager = inputManager;
        _settings = settings;

        RefreshDevices();
        RefreshConfigNames();

        ApplySavedDevice();
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
        ConfigNames.Clear();
        foreach (var name in _settings.ConfigNames)
            ConfigNames.Add(name);

        _suppressCallbacks = true;
        SelectedConfigName = _settings.ActiveConfigName;
        _suppressCallbacks = false;
    }



    private void ApplySavedDevice()
    {
        var savedId = _settings.Active.SelectedDeviceId;

        KeyboardInfo match = NoneDevice;
        if (savedId is not null)
            foreach (var d in AvailableDevices)
                if (d.DevicePath == savedId) { match = d; break; }

        _suppressCallbacks = true;
        SelectedDevice = match;
        _suppressCallbacks = false;

        _inputManager.SetDevice(match?.DevicePath);
    }



    // callbacks



    partial void OnSelectedDeviceChanged(KeyboardInfo? value)
    {
        if (_suppressCallbacks) return;
        if (value is null) return;

        _inputManager.SetDevice(value.DevicePath);
        _settings.Active.SelectedDeviceId = value.DevicePath;
        _settings.Save();
    }



    partial void OnSelectedConfigNameChanged(string? value)
    {
        if (_suppressCallbacks) return;
        if (value is null) return;
        if (value == _settings.ActiveConfigName) return;

        _settings.SwitchConfig(value);
        ApplySavedDevice();
    }



    [RelayCommand]
    private void CreateConfig()
    {
        var name = NewConfigName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        if (_settings.CreateConfig(name, copyFromActive: true))
        {
            RefreshConfigNames();
            SelectedConfigName = name;
            NewConfigName = "";
        }
    }



    [RelayCommand]
    private void DeleteConfig()
    {
        Debug.WriteLine("test");

        if (SelectedConfigName is null) return;
        if (!_settings.DeleteConfig(SelectedConfigName)) return;

        RefreshConfigNames();
        SelectedConfigName = _settings.ActiveConfigName;
    }
}
