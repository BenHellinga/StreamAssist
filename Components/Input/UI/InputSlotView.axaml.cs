using System;
using Avalonia.Controls;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Input.UI;



public partial class InputSlotView : ViewBase
{
    public InputSlotView()
    {
        InitializeComponent();
        CommitOnLeave.Attach(this); // a microphone box's hold applies when its box is left
        DevicePictureHost.SizeChanged += OnKeyboardHostSizeChanged;
    }



    private void OnKeyboardHostSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is InputSlotViewModel vm)
            vm.SetDisplayWidth(e.NewSize.Width);
    }



    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is InputSlotViewModel vm && DevicePictureHost.Bounds.Width > 0)
            vm.SetDisplayWidth(DevicePictureHost.Bounds.Width);
    }
}
