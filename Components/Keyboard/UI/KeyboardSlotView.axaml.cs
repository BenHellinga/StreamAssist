using System;
using Avalonia.Controls;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardSlotView : ViewBase
{
    public KeyboardSlotView()
    {
        InitializeComponent();
        KeyboardHost.SizeChanged += OnKeyboardHostSizeChanged;
    }



    private void OnKeyboardHostSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is KeyboardSlotViewModel vm)
            vm.SetDisplayWidth(e.NewSize.Width);
    }



    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is KeyboardSlotViewModel vm && KeyboardHost.Bounds.Width > 0)
            vm.SetDisplayWidth(KeyboardHost.Bounds.Width);
    }
}
