using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



// every key press from every keyboard, whether or not the app is focused (minimized, another window or monitor, a game
// in front): raw input is registered with RIDEV_INPUTSINK, so Windows sends WM_INPUT to the main window even when it
// isn't the foreground window. Each press says which keyboard it came from (see KeyboardDevices: a keyboard's keys can
// arrive through several connections, they all count as that keyboard). Keyboards plugged in or removed while the app
// runs are noticed (RIDEV_DEVNOTIFY), and keys held when the PC is locked are let go (Windows doesn't send their release)
public class KeyboardInputManager : IDisposable
{
    // private



    private const int WM_INPUT = 0x00FF;
    private const int WM_INPUT_DEVICE_CHANGE = 0x00FE;
    private const int WM_WTSSESSION_CHANGE = 0x02B1;
    private const int WTS_SESSION_LOCK = 0x7;
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(300); // one refresh for a burst of plug messages

    private static IReadOnlyList<InputDeviceModel> _devices = [];

    private Win32.WNDPROC? _wndProcDelegate;
    private IntPtr _originalWndProc;
    private IntPtr _hwnd;
    private Dictionary<string, string> _keyboardOfPath; // a connection's path → its keyboard's main path
    private IDisposable? _pendingRefresh;

    // every key currently held, per keyboard (by its main path)
    private readonly HashSet<(string device, ushort vk)> _keysDown = [];



    // public



    // the keyboards plugged in right now (Key Pressed and the slots' dropdowns list these)
    public static IReadOnlyList<InputDeviceModel> Devices => _devices;



    // events



    public event Action<string, ushort>? KeyDown; // keyboard's main path, key code; first press only, not windows' key repeat
    public event Action<string, ushort>? KeyUp;
    public event Action? DevicesChanged; // a keyboard was plugged in or removed (Devices is up to date)



    // constructor



    public KeyboardInputManager()
    {
        _keyboardOfPath = new(StringComparer.OrdinalIgnoreCase);
        Refresh(raiseChanged: false);
    }



    // public api



    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // InputSink: keys arrive while the app isn't focused (this must stay). DevNotify: plugged in / removed messages
        RawInputDevice.RegisterDevice(
            HidUsageAndPage.Keyboard,
            RawInputDeviceFlags.InputSink | RawInputDeviceFlags.DevNotify,
            _hwnd
        );

        _wndProcDelegate = WndProc;
        _originalWndProc = Win32.SetWindowLongPtr(
            _hwnd,
            Win32.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate)
        );

        Win32.WTSRegisterSessionNotification(_hwnd, Win32.NOTIFY_FOR_THIS_SESSION);
    }



    // true if the key is held on any keyboard (Shift / Ctrl / Alt mean either side)
    public bool IsKeyDown(ushort vk)
    {
        var codes = KeyboardDevices.CodesFor(vk);
        foreach (var (_, heldVk) in _keysDown)
            if (codes.Contains(heldVk)) return true;

        return false;
    }



    // true if the key is held on the given keyboard (by its main path)
    public bool IsKeyDown(string devicePath, ushort vk) => KeyboardDevices.CodesFor(vk).Any(code => _keysDown.Contains((devicePath, code)));



    // raw input management



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
        if (data is not RawInputKeyboardData kb || kb.Device?.DevicePath is not { } path) return;

        // a connection that wasn't there at the last refresh (its plugged in message may still be on its way)
        if (!_keyboardOfPath.TryGetValue(path, out var keyboard))
        {
            ScheduleRefresh();
            return;
        }

        var flags = kb.Keyboard.Flags;
        if (KeyboardDevices.KeyCode(kb.Keyboard.VirutalKey, kb.Keyboard.ScanCode, flags.HasFlag(RawKeyboardFlags.KeyE0)) is not { } vk) return;

        // update the held set before raising the event, so listeners see the new state
        // (windows repeats key down while a key is held, only the first one is passed on)
        if (!flags.HasFlag(RawKeyboardFlags.Up))
        {
            if (_keysDown.Add((keyboard, vk))) KeyDown?.Invoke(keyboard, vk);
        }
        else if (_keysDown.Remove((keyboard, vk)))
        {
            KeyUp?.Invoke(keyboard, vk);
        }
    }



    // keyboards



    private void ScheduleRefresh()
    {
        _pendingRefresh?.Dispose();
        _pendingRefresh = DispatcherTimer.RunOnce(() =>
        {
            _pendingRefresh = null;
            Refresh(raiseChanged: true);
        }, RefreshDelay);
    }



    // reads the keyboards again; ones that kept their main path keep their model (slots and dropdowns hold on to it)
    private void Refresh(bool raiseChanged)
    {
        var groups = KeyboardDevices.Group(KeyboardDevices.Enumerate(), _devices);

        var keyboardOfPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
            foreach (var path in group.Paths)
                keyboardOfPath[path] = group.Device.DevicePath;

        var devices = groups.Select(g => g.Device).ToList();
        var changed = !devices.SequenceEqual(_devices);

        _keyboardOfPath = keyboardOfPath;
        _devices = devices;

        // a removed keyboard's keys can't be released any more
        var connected = devices.Select(d => d.DevicePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ReleaseKeys(device => !connected.Contains(device));

        if (changed && raiseChanged) DevicesChanged?.Invoke();
    }



    // lets go of held keys (raising KeyUp for each), e.g. when the PC is locked or their keyboard is removed
    private void ReleaseKeys(Func<string, bool> ofKeyboard)
    {
        foreach (var (device, vk) in _keysDown.Where(k => ofKeyboard(k.device)).ToList())
        {
            _keysDown.Remove((device, vk));
            KeyUp?.Invoke(device, vk);
        }
    }



    // disposal



    public void Dispose()
    {
        _pendingRefresh?.Dispose();

        if (_hwnd != IntPtr.Zero)
            Win32.WTSUnRegisterSessionNotification(_hwnd);

        if (_hwnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
            Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _originalWndProc);
    }
}
