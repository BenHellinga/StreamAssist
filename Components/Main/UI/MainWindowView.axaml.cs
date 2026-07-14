using System;
using Avalonia.Controls;

namespace StreamAssist.Components.Main.UI;



public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }



    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // init keyboard hook from window handle
        nint hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero) ((MainWindowViewModel)DataContext!).KeyboardComponentManager.InitHwnd(hwnd);
    }



    protected override void OnClosed(EventArgs e)
    {
        ((MainWindowViewModel)DataContext!).Dispose();
        base.OnClosed(e);
    }
}
