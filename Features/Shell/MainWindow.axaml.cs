using System;
using Avalonia.Controls;

namespace StreamAssist.Features.Shell;



public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }



    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // init keyboard hook from window handle
        nint hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
            ((MainWindowViewModel)DataContext!).KeyboardInputManager.Initialize(hwnd);
    }



    protected override void OnClosed(EventArgs e)
    {
        ((MainWindowViewModel)DataContext!).Dispose();
        base.OnClosed(e);
    }
}
