using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StreamAssist.Core.Mvvm;



// a box whose text is applied when it's left (Enter or clicking away), not on every key: its view model keeps the text
// while it's typed and does the work in Commit
public interface ICommitOnLeave
{
    void Commit();
}



// makes the text boxes with the "Commit" class under a view apply their text when they're left: Enter moves the focus
// away, losing focus calls the box's DataContext's Commit(). Doing it here rather than in the box's own Text binding
// lets the view model change the text afterwards (empty a "new ..." box, put back a refused value) and the box shows
// it, since a text box ignores changes to the property while it's writing it
public static class CommitOnLeave
{
    public static void Attach(Control root)
    {
        root.Focusable = true;

        root.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key != Key.Enter || e.Source is not TextBox box || !box.Classes.Contains("Commit")) return;

            root.Focus();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        root.AddHandler(InputElement.LostFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox box && box.Classes.Contains("Commit") && box.DataContext is ICommitOnLeave target)
                target.Commit();
        }, RoutingStrategies.Bubble);
    }
}
