using System;
using Avalonia.Controls;

namespace StreamAssist.Components.Keyboard.UI;



public partial class KeyboardSlotView : UserControl
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
