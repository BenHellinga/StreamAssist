using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



// TODO gotta redo this to include pluggin/unplugged callbacks and more device types
public class KeyboardInputManager : IDisposable
{
    // TODO GetAvailableKeyboards should probably be private, then setup callbacks for plugging/unplugging keyboards to update this reference
    public static readonly List<InputDeviceModel> Devices = GetAvailableKeyboards();

    private Win32.WNDPROC? _wndProcDelegate;
    private IntPtr _originalWndProc;
    private IntPtr _hwnd;

    private const int WM_INPUT = 0x00FF;

    // every key currently held, per keyboard (left/right shift, ctrl and alt share one key code)
    private readonly HashSet<(string device, ushort vk)> _keysDown = [];



    // events



    public event Action<string, ushort>? KeyDown; // first press only, not windows' key repeat
    public event Action<string, ushort>? KeyUp;



    // public api



    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;

        RawInputDevice.RegisterDevice(
            HidUsageAndPage.Keyboard,
            RawInputDeviceFlags.InputSink,
            _hwnd
        );

        _wndProcDelegate = WndProc;
        _originalWndProc = Win32.SetWindowLongPtr(
            _hwnd,
            Win32.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate)
        );
    }



    // true if the key is held on any keyboard
    public bool IsKeyDown(ushort vk)
    {
        foreach (var (_, heldVk) in _keysDown)
            if (heldVk == vk) return true;

        return false;
    }



    // true if the key is held on the given keyboard
    public bool IsKeyDown(string devicePath, ushort vk) => _keysDown.Contains((devicePath, vk));



    public static List<InputDeviceModel> GetAvailableKeyboards()
    {
        var results = new List<InputDeviceModel>();
        var seen = new HashSet<(int, int)>();

        foreach (RawInputDevice? device in RawInputDevice.GetDevices())
        {
            if (device?.DevicePath is null) continue;
            if (device.DeviceType != RawInputDeviceType.Keyboard) continue;
            if (device.DevicePath.Contains("&Col")) continue;
            if (!seen.Add((device.VendorId, device.ProductId))) continue;

            results.Add(new InputDeviceModel(
                devicePath: device.DevicePath,
                manufacturerName: device.ManufacturerName ?? "Unknown",
                productName: device.ProductName ?? "Unknown",
                vendorId: device.VendorId,
                productId: device.ProductId
            ));
        }

        return results;
    }



    // raw input management



    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUT)
        {
            var data = RawInputData.FromHandle(lParam);
            if (data is RawInputKeyboardData kb && kb.Device?.DevicePath is { } path)
            {
                var vk     = (ushort)kb.Keyboard.VirutalKey;
                var isDown = !kb.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up);

                // update the held set before raising the event, so listeners see the new state
                // (windows repeats key down while a key is held, only the first one is passed on)
                if (isDown)
                {
                    if (_keysDown.Add((path, vk))) KeyDown?.Invoke(path, vk);
                }
                else
                {
                    _keysDown.Remove((path, vk));
                    KeyUp?.Invoke(path, vk);
                }
            }
        }

        return Win32.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }



    // disposal



    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
            Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _originalWndProc);
    }
}