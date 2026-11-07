using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



public partial class WidgetView : ViewBase
{
    public WidgetView()
    {
        InitializeComponent();
        CommitOnLeave.Attach(this); // the name, settings and option boxes apply when they're left
    }
}
