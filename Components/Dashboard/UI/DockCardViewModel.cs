using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a kind of dock in the dock list, dragging it onto the dashboard adds one (like a block card)
public sealed class DockCardViewModel(DockKind kind) : ViewModelBase, IDraggable
{
    public string Name => DockModel.KindTitle(kind);

    public string Description => kind switch
    {
        DockKind.Console => "Shows the messages of Dock Message blocks that name it",
        DockKind.Timer => "Its own timers (Start Timer blocks that name it), soonest first, to pause or stop them",
        DockKind.Sound => "Sounds and speech sent to it (Play Sound, Speak Text), to skip or stop them: one at a time in order (Wait Until Done) or over each other",
        DockKind.Button => "Buttons you name, each running its own binding when pressed",
        DockKind.Variable => "Variables by name, to read or type their values ({var.name} and Set Variable use the same ones)",
        DockKind.Indicator => "Names you type, green or red as Set Indicator blocks naming this dock turn them on or off (e.g. muted)",
        DockKind.Blank => "An empty spot: paste a copied dock into it (its paste button shows on hover)",
        _ => "",
    };



    public object? GetDragPayload() => new NewDockPayload(kind);



    public override void Dispose() { }
}
