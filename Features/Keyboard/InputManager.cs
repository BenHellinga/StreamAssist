using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;

namespace StreamAssist.Features.Keyboard;



public class InputManager : IDisposable
{
    private string? _selectedKeyboardId;
    private bool _disposed;

    private Win32.WNDPROC? _wndProcDelegate;
    private IntPtr _originalWndProc;
    private IntPtr _hwnd;

    private const int WM_INPUT = 0x00FF;



    // events



    public event Action<ushort>? KeyPressed;
    public event Action<ushort>? KeyReleased;



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



    public void SetDevice(string? keyboardId) => _selectedKeyboardId = keyboardId;



    public static IReadOnlyList<KeyboardInfo> GetAvailableKeyboards()
    {
        var results = new List<KeyboardInfo>();
        var seen = new HashSet<(int, int)>();

        foreach (RawInputDevice? device in RawInputDevice.GetDevices())
        {
            if (device?.DevicePath is null) continue;
            if (device.DeviceType != RawInputDeviceType.Keyboard) continue;
            if (!seen.Add((device.VendorId, device.ProductId))) continue;
            
            results.Add(new KeyboardInfo(
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
        if (msg == WM_INPUT && _selectedKeyboardId is not null)
        {
            var data = RawInputData.FromHandle(lParam);
            if (data is RawInputKeyboardData kb &&
                kb.Device?.DevicePath == _selectedKeyboardId)
            {
                var vk     = (ushort)kb.Keyboard.VirutalKey;
                var isDown = !kb.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up);

                if (isDown) KeyPressed?.Invoke(vk);
                else        KeyReleased?.Invoke(vk);
            }
        }

        return Win32.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }



    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        if (_hwnd != IntPtr.Zero && _originalWndProc != IntPtr.Zero)
            Win32.SetWindowLongPtr(_hwnd, Win32.GWLP_WNDPROC, _originalWndProc);
    }
}