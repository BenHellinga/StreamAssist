using Avalonia.Input;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class BlockView : ViewBase
{
    public BlockView()
    {
        InitializeComponent();
    }



    // clicking a collapsed block expands it
    private void OnCollapsedPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not BlockViewModel vm) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        vm.ExpandPressedCommand.Execute(null);
        e.Handled = true;
    }
}
