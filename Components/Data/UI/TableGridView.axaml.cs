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
}
