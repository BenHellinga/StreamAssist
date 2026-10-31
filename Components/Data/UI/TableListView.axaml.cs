using Avalonia.Controls;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Data.UI;



public partial class TableListView : UserControl
{
    public TableListView()
    {
        InitializeComponent();

        // the new table box makes its table when it's left (Enter or clicking away)
        CommitOnLeave.Attach(this);
    }
}
