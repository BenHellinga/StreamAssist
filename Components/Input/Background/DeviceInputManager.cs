using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.Background;



// every press from every input device, whether or not the app is focused (minimized, another window or monitor, a game
// in front). Keyboards and mice come through raw input, registered with RIDEV_INPUTSINK, so Windows sends WM_INPUT to
// the main window even when it isn't the foreground window; each press says which device it came from (see
// InputDevices: a device's input can arrive through several connections, they all count as that device). Controllers
// (XInput) are polled. Everything is a key code (mouse buttons and the wheel, controller buttons, triggers and stick
// directions too, see InputDevices). Devices plugged in or removed while the app runs are noticed (RIDEV_DEVNOTIFY, and
// the controller polling), and what's held when the PC is locked is let go (Windows doesn't send those releases)
public class DeviceInputManager : IDisposable
{
    // private



    private const int WM_INPUT = 0x00FF;
    private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOCK = 0x7;
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(300); // one refresh for a burst of plug messages
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16); // controllers, about 60 times a second
    private const int ProbeEvery = 60; // an empty controller slot is only asked about this often (XInput is slow there)

    private static IReadOnlyList<InputDeviceModel> _devices = [];

    private Win32.WNDPROC? _wndProcDelegate;
    private IntPtr _originalWndProc;
    private IntPtr _hwnd;
    private Dictionary<string, string> _deviceOfPath; // a connection's path → its device's main path
    private List<InputDeviceModel> _rawDevices; // the keyboards and mice, from the last raw input refresh
    private IDisposable? _pendingRefresh;

    private readonly InputDeviceModel?[] _controllers; // by XInput slot, the model kept while it's plugged in
    private readonly Microphones _microphones; // the audio inputs, read again when windows says they changed
    private List<InputDeviceModel> _mics;
    private readonly HashSet<ushort>[] _controllerHeld; // what each controller held at the last poll
    private readonly DispatcherTimer _pollTimer;
    private int _pollCount;

    // everything currently held, per device (by its main path)
    private readonly HashSet<(string device, ushort vk)> _keysDown = [];



    // public



    // the devices plugged in right now (the blocks and the boxes' dropdowns list these, a box only its own kind)
    public static IReadOnlyList<InputDeviceModel> Devices => _devices;



    // events



    public event Action<string, ushort>? KeyDown; // device's main path, key code; first press only, not windows' key repeat
    public event Action<string, ushort>? KeyUp;
    public event Action? DevicesChanged; // a device was plugged in or removed (Devices is up to date)



    // constructor



    public DeviceInputManager()
    {
        _deviceOfPath = new(StringComparer.OrdinalIgnoreCase);
        _rawDevices = [];
        _controllers = new InputDeviceModel?[XInput.MaxControllers];
        _controllerHeld = Enumerable.Range(0, XInput.MaxControllers).Select(_ => new HashSet<ushort>()).ToArray();
        _microphones = new(OnMicrophonesChanged);
        _mics = _microphones.Enumerate([]);

        RefreshRaw();
        ProbeControllers();
        PublishDevices(raiseChanged: false);

        _pollTimer = new DispatcherTimer { Interval = PollInterval };
        _pollTimer.Tick += OnPoll;
        _pollTimer.Start();
    }



    // public api



    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // InputSink: input arrives while the app isn't focused (this must stay). DevNotify: plugged in / removed messages
        RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink | RawInputDeviceFlags.DevNotify, _hwnd);
        RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink | RawInputDeviceFlags.DevNotify, _hwnd);

        _wndProcDelegate = WndProc;
        _originalWndProc = Win32.SetWindowLongPtr(
            _hwnd,
            Win32.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate)
        );

        Win32.WTSRegisterSessionNotification(_hwnd, Win32.NOTIFY_FOR_THIS_SESSION);
    }



    // true if the key / button is held on any device (Shift / Ctrl / Alt mean either side)
    public bool IsKeyDown(ushort vk)
    {
        var codes = InputDevices.CodesFor(vk);
        foreach (var (_, heldVk) in _keysDown)
            if (codes.Contains(heldVk)) return true;

        return false;
    }



    // true if the key / button is held on the given device (by its main path)
    public bool IsKeyDown(string devicePath, ushort vk) => InputDevices.CodesFor(vk).Any(code => _keysDown.Contains((devicePath, code)));



    // raw input (keyboards, mice)



    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INPUT:
                OnInput(lParam);
                break;
            case WM_INPUT_DEVICE_CHANGE:
                ScheduleRefresh();
                break;
            case WM_WTSSESSION_CHANGE when wParam == WTS_SESSION_LOCK:
                ReleaseKeys(_ => true);
                break;
        }

        return Win32.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }



    private void OnInput(IntPtr lParam)
    {
        var data = RawInputData.FromHandle(lParam);
        if (data is RawInputKeyboardData kb) OnKeyboardInput(kb);
        else if (data is RawInputMouseData mouse) OnMouseInput(mouse);
    }



    private void OnKeyboardInput(RawInputKeyboardData kb)
    {
        if (DeviceOf(kb.Device?.DevicePath) is not { } keyboard) return;

        var flags = kb.Keyboard.Flags;
        if (InputDevices.KeyCode(kb.Keyboard.VirutalKey, kb.Keyboard.ScanCode, flags.HasFlag(RawKeyboardFlags.KeyE0)) is not { } vk) return;

        // windows repeats key down while a key is held, only the first one is passed on
        if (!flags.HasFlag(RawKeyboardFlags.Up)) Press(keyboard, vk);
        else Release(keyboard, vk);
    }



    // a mouse sends a message for every bit of movement: those (no buttons, no wheel) are skipped straight away. Input
    // that doesn't say which mouse it's from (mouse software, e.g. Logitech's, or smooth scrolling, sends the wheel
    // itself, with no device) or comes through a connection that isn't one of the mice counts for every mouse
    private void OnMouseInput(RawInputMouseData data)
    {
        var buttons = data.Mouse.Buttons;
        if (buttons == RawMouseButtonFlags.None) return;

        var path = data.Device?.DevicePath;
        IEnumerable<string> mice = path != null && _deviceOfPath.TryGetValue(path, out var mouse)
            ? [mouse]
            : _devices.Where(d => d.Kind == DeviceKind.Mouse).Select(d => d.DevicePath).ToList();
        if (path != null && !_deviceOfPath.ContainsKey(path)) ScheduleRefresh(); // maybe just plugged in

        foreach (var (code, isDown) in InputDevices.MouseCodes(buttons, data.Mouse.ButtonData))
            foreach (var device in mice)
            {
                if (isDown) Press(device, code);
                else Release(device, code);
            }
    }



    // the device a connection belongs to; one that wasn't there at the last refresh (its plugged in message may still be
    // on its way) is looked for again
    private string? DeviceOf(string? path)
    {
        if (path == null) return null;
        if (_deviceOfPath.TryGetValue(path, out var device)) return device;

        ScheduleRefresh();
        return null;
    }



    // the held set is updated before the event, so listeners see the new state
    private void Press(string device, ushort vk)
    {
        if (_keysDown.Add((device, vk))) KeyDown?.Invoke(device, vk);
    }



    private void Release(string device, ushort vk)
    {
        if (_keysDown.Remove((device, vk))) KeyUp?.Invoke(device, vk);
    }



    // controllers (XInput)



    // each controller's held buttons against the last poll; empty slots are only asked about every ProbeEvery polls
    private void OnPoll(object? sender, EventArgs e)
    {
        var probe = ++_pollCount % ProbeEvery == 0;
        var changed = false;

        for (var i = 0; i < XInput.MaxControllers; i++)
        {
            if (_controllers[i] == null && !probe) continue;

            if (!XInput.TryGetState(i, out var pad))
            {
                if (_controllers[i] is { } gone)
                {
                    SetControllerHeld(i, gone.DevicePath, []);
                    _controllers[i] = null;
                    changed = true;
                }
                continue;
            }

            if (_controllers[i] == null)
            {
                _controllers[i] = NewController(i);
                changed = true;
            }

            var held = InputDevices.ControllerCodes(pad.Buttons, pad.LeftTrigger, pad.RightTrigger, pad.ThumbLX, pad.ThumbLY, pad.ThumbRX, pad.ThumbRY);
            SetControllerHeld(i, _controllers[i]!.DevicePath, held);
        }

        if (changed) PublishDevices(raiseChanged: true);
    }



    private void ProbeControllers()
    {
        for (var i = 0; i < XInput.MaxControllers; i++)
            if (XInput.TryGetState(i, out _)) _controllers[i] = NewController(i);
    }



    private static InputDeviceModel NewController(int index) =>
        new($"xinput:{index + 1}", $"Controller {index + 1}", "XInput", 0, 0, DeviceKind.Controller);



    private void SetControllerHeld(int index, string device, HashSet<ushort> held)
    {
        var was = _controllerHeld[index];
        foreach (var code in was.Except(held).ToList()) Release(device, code);
        foreach (var code in held.Except(was).ToList()) Press(device, code);
        _controllerHeld[index] = held;
    }



    // devices



    private void ScheduleRefresh()
    {
        _pendingRefresh?.Dispose();
        _pendingRefresh = DispatcherTimer.RunOnce(() =>
        {
            _pendingRefresh = null;
            RefreshRaw();
            PublishDevices(raiseChanged: true);
        }, RefreshDelay);
    }



    // reads the keyboards and mice again; ones that kept their main path keep their model (slots and dropdowns hold on
    // to it)
    private void RefreshRaw()
    {
        var groups = InputDevices.Group(InputDevices.Enumerate(), _devices);

        var deviceOfPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
            foreach (var path in group.Paths)
                deviceOfPath[path] = group.Device.DevicePath;

        _deviceOfPath = deviceOfPath;
        _rawDevices = groups.Select(g => g.Device).ToList();
    }



    private void OnMicrophonesChanged()
    {
        _mics = _microphones.Enumerate(_mics);
        PublishDevices(raiseChanged: true);
    }



    // the keyboards and mice, then the controllers, then the microphones; anything held on a device that's gone is let go
    private void PublishDevices(bool raiseChanged)
    {
        var devices = _rawDevices.Concat(_controllers.OfType<InputDeviceModel>()).Concat(_mics).ToList();
        var changed = !devices.SequenceEqual(_devices);
        _devices = devices;

        var connected = devices.Select(d => d.DevicePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ReleaseKeys(device => !connected.Contains(device));

        if (changed && raiseChanged) DevicesChanged?.Invoke();
    }



    // lets go of held keys (raising KeyUp for each), e.g. when the PC is locked or their device is removed
    private void ReleaseKeys(Func<string, bool> ofDevice)
    {
        foreach (var (device, vk) in _keysDown.Where(k => ofDevice(k.device)).ToList())
            Release(device, vk);

        // a controller's buttons are pressed again on the next poll if they're still held
        for (var i = 0; i < XInput.MaxControllers; i++)
            if (_controllers[i] is { } pad && ofDevice(pad.DevicePath)) _controllerHeld[i] = [];
    }



    // disposal



    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Tick -= OnPoll;
        _microphones.Dispose();
        _pendingRefresh?.Dispose();

        if (_hwnd != IntPtr.Zero)
            Win32.WTSUnRegisterSessionNotification(_hwnd);

        if (_hwnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
            Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _originalWndProc);
    }
}
