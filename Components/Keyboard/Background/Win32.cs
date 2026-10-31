using System;
using System.Runtime.InteropServices;

namespace StreamAssist.Components.Keyboard.Background;



internal static class Win32
{
    public const int GWLP_WNDPROC = -4;
    public const int NOTIFY_FOR_THIS_SESSION = 0;



    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate IntPtr WNDPROC(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);



    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);



    // WM_WTSSESSION_CHANGE (lock / unlock) for this window
    [DllImport("wtsapi32.dll")]
    public static extern bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);

    [DllImport("wtsapi32.dll")]
    public static extern bool WTSUnRegisterSessionNotification(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}