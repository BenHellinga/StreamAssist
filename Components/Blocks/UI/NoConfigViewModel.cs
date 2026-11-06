using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// what a tab with configs shows while it has none: nothing can be bound until one is made in the bar above (or another
// empty page's note, e.g. a dashboard with no docks yet)
public class NoConfigViewModel(string message = "Create a config") : ViewModelBase
{
    public string Message => message;



    public override void Dispose() { }
}
