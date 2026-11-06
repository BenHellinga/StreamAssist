using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Keyboard.Model;



// contains the data for a single keyboard slot (an input device box on the Keyboard tab): its keyboard, its layout
// (picked when it's made: a keyboard picture, or Generic, every key code), and whether it's off (its keys run nothing)
// or collapsed (only its header shows). Its keys belong to the tab's config in use (KeyboardConfigModel, found by the
// slot's id), which the list hands it through ShowConfig
public class KeyboardSlotModel : ModelBase
{
    // constants



    // the pictures a slot can show its keys in: a keyboard, or every key code Windows has (F13-F24, media keys, ...).
    // Binds belong to key codes, so a bind shows in every layout that has its key
    public const string KeyboardLayout = "Keyboard";
    public const string AllKeysLayout = "All keys";
    public static readonly IReadOnlyList<string> Layouts = [KeyboardLayout, AllKeysLayout];



    // private



    private IReadOnlyList<InputDeviceModel>? _devices;
    private int _id;

    private InputDeviceModel? _selectedDevice;
    private KeybindConfigModel? _selectedConfig;
    private string? _pendingDeviceString;
    private string _layout;
    private bool _isDisabled;
    private bool _isCollapsed;



    // public



    [JsonIgnore] public int Id => _id; // which keys it has in each config
    [JsonIgnore] public InputDeviceModel? SelectedDevice => _selectedDevice;
    [JsonIgnore] public KeybindConfigModel? SelectedConfig => _selectedConfig;
    [JsonIgnore] public string Layout => _layout;
    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public bool IsCollapsed => _isCollapsed;



    // json



    [JsonInclude] [JsonPropertyName("selectedDevice")]
    private string? JsonSelectedDevice
    {
        get => _selectedDevice?.DevicePath ?? _pendingDeviceString;
        set => _pendingDeviceString = value; // defer validation until device list is attached
    }

    [JsonInclude] [JsonPropertyName("id")]
    private int JsonId
    {
        get => _id;
        set => _id = value;
    }

    [JsonInclude] [JsonPropertyName("layout")]
    private string JsonLayout
    {
        get => _layout;
        set => _layout = Layouts.Contains(value) ? value : KeyboardLayout;
    }

    [JsonInclude] [JsonPropertyName("isDisabled")]
    private bool JsonIsDisabled
    {
        get => _isDisabled;
        set => _isDisabled = value;
    }

    [JsonInclude] [JsonPropertyName("isCollapsed")]
    private bool JsonIsCollapsed
    {
        get => _isCollapsed;
        set => _isCollapsed = value;
    }



    // events



    public event Action<InputDeviceModel?, InputDeviceModel?>? SelectedDeviceChanged;
    public event Action<KeybindConfigModel?, KeybindConfigModel?>? SelectedConfigChanged; // the tab switched configs
    public event Action<string>? LayoutChanged;
    public event Action<bool>? IsDisabledChanged;
    public event Action<bool>? IsCollapsedChanged;



    // constructors



    public KeyboardSlotModel()
    {
        _devices = null;
        _id = 0; // given one by the list once loaded

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _layout = KeyboardLayout;
        _isDisabled = false;
        _isCollapsed = false;
    }



    public KeyboardSlotModel(IReadOnlyList<InputDeviceModel>? devices, int id, string layout = KeyboardLayout)
    {
        _devices = devices;
        _id = id;

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _layout = layout;
        _isDisabled = false;
        _isCollapsed = false;
    }



    // attach methods



    // the keyboards were read (at startup) or changed (one was plugged in or removed): find this slot's keyboard again by
    // its path. A missing one leaves the slot without a keyboard but keeps its path (saved, and picked up again when it's
    // plugged back in), with one warning: at startup that it's not there, later that it was removed
    public void AttachDevices(IReadOnlyList<InputDeviceModel> devices)
    {
        var firstTime = _devices == null;
        _devices = devices;

        var wanted = _selectedDevice?.DevicePath ?? _pendingDeviceString;
        if (wanted == null) return;

        var device = _devices.FirstOrDefault(d => string.Equals(d.DevicePath, wanted, StringComparison.OrdinalIgnoreCase));
        if (device == null)
        {
            if (firstTime) AppLog.WriteWarning($"keyboard '{wanted}' not found");
            else if (_selectedDevice != null) AppLog.WriteWarning($"keyboard removed ({_selectedDevice.ProductName})");

            _pendingDeviceString = wanted; // kept until a keyboard is picked
            SetSelectedDevice(null);
            return;
        }

        _pendingDeviceString = null;
        SetSelectedDevice(device);
    }



    // layout



    public bool TrySetLayout(string layout)
    {
        if (!Layouts.Contains(layout))
        {
            AppLog.WriteError($"no keyboard layout '{layout}'");
            return false;
        }

        if (layout == _layout) return true;

        _layout = layout;
        LayoutChanged?.Invoke(_layout);
        return true;
    }



    // on / off, collapsed



    // an off slot's keys run nothing (the KeybindManager skips it); they still light up when pressed
    public void SetIsDisabled(bool isDisabled)
    {
        if (isDisabled == _isDisabled) return;

        _isDisabled = isDisabled;
        IsDisabledChanged?.Invoke(_isDisabled);
    }



    public void SetIsCollapsed(bool isCollapsed)
    {
        if (isCollapsed == _isCollapsed) return;

        _isCollapsed = isCollapsed;
        IsCollapsedChanged?.Invoke(_isCollapsed);
    }



    // device management



    // picking a keyboard (or none) replaces the one that was waiting to be plugged back in
    public bool TrySetSelectedDevice(InputDeviceModel? device)
    {
        if (device == null)
        {
            _pendingDeviceString = null;
            SetSelectedDevice(device);
            return true;
        }

        if (!_devices?.Contains(device) ?? false)
        {
            AppLog.WriteError($"keyboard '{device.DevicePath}' not found");
            return false;
        }

        _pendingDeviceString = null;
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



    // config



    // only the list sets these: the slot's id (once, for slots saved without one) and its keys in the config in use
    internal void SetId(int id) => _id = id;



    internal void ShowConfig(KeybindConfigModel? newConfig)
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
    }
}