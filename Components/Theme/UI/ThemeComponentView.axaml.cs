using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Theme.UI;



public partial class ThemeComponentView : ViewBase
{
    public ThemeComponentView()
    {
        InitializeComponent();
        CommitOnLeave.Attach(this); // values apply when their box is left
    }
}
