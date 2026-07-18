using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.Keyboard.Background;



public class KeyboardInputManager : IDisposable
{
    // TODO GetAvailableKeyboards should probably be private, then setup callbacks for plugging/unplugging keyboards to update this reference
    public static readonly List<KeyboardDeviceModel> Devices = GetAvailableKeyboards();

    private Win32.WNDPROC? _wndProcDelegate;
    private IntPtr _originalWndProc;
    private IntPtr _hwnd;

    private const int WM_INPUT = 0x00FF;



    // events



    public event Action<string, ushort>? KeyDown;
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



    public static List<KeyboardDeviceModel> GetAvailableKeyboards()
    {
        var results = new List<KeyboardDeviceModel>();
        var seen = new HashSet<(int, int)>();

        foreach (RawInputDevice? device in RawInputDevice.GetDevices())
        {
            if (device?.DevicePath is null) continue;
            if (device.DeviceType != RawInputDeviceType.Keyboard) continue;
            if (device.DevicePath.Contains("&Col")) continue;
            if (!seen.Add((device.VendorId, device.ProductId))) continue;

            results.Add(new KeyboardDeviceModel(
                DevicePath: device.DevicePath,
                ManufacturerName: device.ManufacturerName ?? "Unknown",
                ProductName: device.ProductName ?? "Unknown",
                VendorId: device.VendorId,
                ProductId: device.ProductId
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

                if (isDown) KeyDown?.Invoke(path, vk);
                else        KeyUp?.Invoke(path, vk);
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