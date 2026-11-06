using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Input.Model;



// contains the data for a single input device box on the Input tab: its device, its layout (picked when it's made: a
// keyboard picture, Generic (every key code), a mouse or a controller, which decides the kind of device it takes), and
// whether it's off (its keys run nothing)
// or collapsed (only its header shows). Its keys belong to the tab's config in use (InputConfigModel, found by the
// slot's id), which the list hands it through ShowConfig
public class InputSlotModel : ModelBase
{
    // constants



    // the pictures a slot can show its keys in: a keyboard, every key code Windows has (F13-F24, media keys, ...), a
    // mouse or a controller. Binds belong to key codes, so a bind shows in every layout that has its key
    public const string KeyboardLayout = "Keyboard";
    public const string AllKeysLayout = "All keys";
    public const string MouseLayout = "Mouse";
    public const string ControllerLayout = "Controller";
    public const string MicrophoneLayout = "Microphone";
    public static readonly IReadOnlyList<string> Layouts = [KeyboardLayout, AllKeysLayout, MouseLayout, ControllerLayout, MicrophoneLayout];

    // a microphone box: talking starts when the level reaches the threshold, stops once it's been under it this long
    public const double DefaultMicThreshold = -35; // dB below full scale
    public const int DefaultMicHold = 300; // ms
    public const double MinMicThreshold = -80;
    public const int MaxMicHold = 5000;



    // the kind of device a layout takes (null: any, a Generic box's grid has every code a keyboard, mouse or controller
    // sends)
    public static Model.DeviceKind? KindOf(string layout) => layout switch
    {
        KeyboardLayout => Model.DeviceKind.Keyboard,
        MouseLayout => Model.DeviceKind.Mouse,
        ControllerLayout => Model.DeviceKind.Controller,
        MicrophoneLayout => Model.DeviceKind.Microphone,
        _ => null,
    };



    // private



    private IReadOnlyList<InputDeviceModel>? _devices;
    private int _id;

    private InputDeviceModel? _selectedDevice;
    private KeybindConfigModel? _selectedConfig;
    private string? _pendingDeviceString;
    private string _layout;
    private bool _isDisabled;
    private bool _isCollapsed;
    private double _micThreshold;
    private int _micHold;
    private double _micLevel; // this session: the input's level now, dB
    private bool _isTalking; // this session



    // public



    [JsonIgnore] public int Id => _id; // which keys it has in each config
    [JsonIgnore] public InputDeviceModel? SelectedDevice => _selectedDevice;
    [JsonIgnore] public KeybindConfigModel? SelectedConfig => _selectedConfig;
    [JsonIgnore] public string Layout => _layout;
    [JsonIgnore] public DeviceKind? DeviceKind => KindOf(_layout); // the devices its dropdown lists (null: any)
    private string What => DeviceKind?.ToString().ToLowerInvariant() ?? "device"; // for messages
    [JsonIgnore] public bool IsDisabled => _isDisabled;
    [JsonIgnore] public bool IsCollapsed => _isCollapsed;
    [JsonIgnore] public double MicThreshold => _micThreshold;
    [JsonIgnore] public int MicHold => _micHold;
    [JsonIgnore] public double MicLevel => _micLevel;
    [JsonIgnore] public bool IsTalking => _isTalking;



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

    // only written for microphone boxes
    [JsonInclude] [JsonPropertyName("micThreshold")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private double? JsonMicThreshold
    {
        get => _layout == MicrophoneLayout ? _micThreshold : null;
        set => _micThreshold = value is >= MinMicThreshold and <= 0 ? value.Value : DefaultMicThreshold;
    }

    [JsonInclude] [JsonPropertyName("micHold")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? JsonMicHold
    {
        get => _layout == MicrophoneLayout ? _micHold : null;
        set => _micHold = value is >= 0 and <= MaxMicHold ? value.Value : DefaultMicHold;
    }



    // events



    public event Action<InputDeviceModel?, InputDeviceModel?>? SelectedDeviceChanged;
    public event Action<KeybindConfigModel?, KeybindConfigModel?>? SelectedConfigChanged; // the tab switched configs
    public event Action<string>? LayoutChanged;
    public event Action<bool>? IsDisabledChanged;
    public event Action<bool>? IsCollapsedChanged;
    public event Action? MicSettingsChanged; // the threshold or hold (saved)
    public event Action<double>? MicLevelChanged; // this session
    public event Action<bool>? IsTalkingChanged; // this session



    // constructors



    public InputSlotModel()
    {
        _devices = null;
        _id = 0; // given one by the list once loaded

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _layout = KeyboardLayout;
        _isDisabled = false;
        _isCollapsed = false;
        _micThreshold = DefaultMicThreshold;
        _micHold = DefaultMicHold;
        _micLevel = double.NegativeInfinity;
        _isTalking = false;
    }



    public InputSlotModel(IReadOnlyList<InputDeviceModel>? devices, int id, string layout = KeyboardLayout)
    {
        _devices = devices;
        _id = id;

        _selectedDevice = null;
        _selectedConfig = null;
        _pendingDeviceString = null;
        _layout = layout;
        _isDisabled = false;
        _isCollapsed = false;
        _micThreshold = DefaultMicThreshold;
        _micHold = DefaultMicHold;
        _micLevel = double.NegativeInfinity;
        _isTalking = false;
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
            if (firstTime) AppLog.WriteWarning($"{What} '{wanted}' not found");
            else if (_selectedDevice != null) AppLog.WriteWarning($"{What} removed ({_selectedDevice.ProductName})");

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
            AppLog.WriteError($"no input device layout '{layout}'");
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



    // a microphone box's threshold (dB, MinMicThreshold to 0) and hold (ms, 0 to MaxMicHold)
    public bool TrySetMicThreshold(double threshold)
    {
        if (!(threshold >= MinMicThreshold && threshold <= 0))
        {
            AppLog.WriteError($"can't set talking threshold, must be {MinMicThreshold} to 0 dB ({threshold})");
            return false;
        }

        if (threshold == _micThreshold) return true;
        _micThreshold = threshold;
        MicSettingsChanged?.Invoke();
        return true;
    }



    public bool TrySetMicHold(int hold)
    {
        if (hold is < 0 or > MaxMicHold)
        {
            AppLog.WriteError($"can't set talking hold, must be 0 to {MaxMicHold} ms ({hold})");
            return false;
        }

        if (hold == _micHold) return true;
        _micHold = hold;
        MicSettingsChanged?.Invoke();
        return true;
    }



    // what the MicrophoneManager measured and decided (this session only)
    public void SetMicLevel(double level)
    {
        if (level == _micLevel) return;
        _micLevel = level;
        MicLevelChanged?.Invoke(level);
    }



    public void SetIsTalking(bool isTalking)
    {
        if (isTalking == _isTalking) return;
        _isTalking = isTalking;
        IsTalkingChanged?.Invoke(isTalking);
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
            AppLog.WriteError($"{What} '{device.DevicePath}' not found");
            return false;
        }

        if (DeviceKind is { } kind ? device.Kind != kind : device.Kind == Model.DeviceKind.Microphone)
        {
            AppLog.WriteError($"can't pick a {device.Kind.ToString().ToLowerInvariant()} for a {What} box ({device.ProductName})");
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