using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
 
namespace StreamAssist.Components.Main.UI;
 
 
 
public partial class MainWindowView : Window
{
    public MainWindowView()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
 
        AddHandler(PointerPressedEvent, OnBackgroundPointerPressed, RoutingStrategies.Tunnel);
    }
 
 
 
    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is InputElement { Focusable: true }) return;
        FocusManager?.Focus(null);
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