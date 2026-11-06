using Avalonia.Controls;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Data.UI;



public partial class TableGridView : UserControl
{
    public TableGridView()
    {
        InitializeComponent();

        // cells, column names and the new row / column boxes apply their text when they're left (Enter or clicking away)
        CommitOnLeave.Attach(this);
    }



    // a column header was laid out: its cells take its width (so they never grow with their own text)
    private void OnHeaderSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is Control { DataContext: ColumnHeaderViewModel header }) header.Width = e.NewSize.Width;
    }
}
