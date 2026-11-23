using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



public partial class BindableEditorView : ViewBase
{
    public BindableEditorView()
    {
        InitializeComponent();
        CommitOnLeave.Attach(this); // the script's limit boxes apply when they're left
    }
}
