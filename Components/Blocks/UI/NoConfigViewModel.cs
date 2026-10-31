using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// what a tab with configs shows while it has none: nothing can be bound until one is made in the bar above
public class NoConfigViewModel : ViewModelBase
{
    public string Message => "Create a config";



    public override void Dispose() { }
}
