using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a button dock's body: its rows (the last one always empty, typing a name in it adds the next), following the model.
// Dragging a row's binding onto another row moves the whole row (name and binding) before that one
public sealed partial class ButtonDockViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly DockModel _dock;



    // public



    public ObservableCollection<DockButtonRowViewModel> Rows { get; }



    // constructor



    public ButtonDockViewModel(DockModel dock)
    {
        _dock = dock;
        Rows = new(_dock.Buttons.Rows.Select(row => new DockButtonRowViewModel(_dock, row, this)));

        _dock.Buttons.RowInserted += OnRowInserted;
        _dock.Buttons.RowRemoved += OnRowRemoved;
        _dock.Buttons.RowMoved += OnRowMoved;
    }



    // header button



    [RelayCommand] private void OnPressAllPressed() => _dock.PressAllButtons();



    // drag drop



    // only reordering: the binding of one of this dock's rows
    public bool CanAcceptDropPayload(object payload) => FindRow(payload) != null;



    // anything else goes on to the dock around (e.g. a dock dropped beside this one)
    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindRow(payload) is not { } row) return;
        if (zoneContext is not DockButtonRowViewModel before) return;

        _dock.Buttons.TryMoveRow(row, Rows.IndexOf(before));
    }



    // model events



    private void OnRowInserted(DockButton row, int index) => Rows.Insert(index, new DockButtonRowViewModel(_dock, row, this));



    private void OnRowMoved(DockButton _, int from, int to) => Rows.Move(from, to);



    private void OnRowRemoved(DockButton row)
    {
        for (var i = 0; i < Rows.Count; i++)
        {
            if (!Rows[i].Represents(row)) continue;

            Rows[i].Dispose();
            Rows.RemoveAt(i);
            return;
        }
    }



    // helpers



    private DockButton? FindRow(object payload) =>
        payload is BindableDragPayload dragged ? _dock.Buttons.Rows.FirstOrDefault(r => r.Bindable == dragged.Bindable) : null;



    // disposal



    public override void Dispose()
    {
        _dock.Buttons.RowInserted -= OnRowInserted;
        _dock.Buttons.RowRemoved -= OnRowRemoved;
        _dock.Buttons.RowMoved -= OnRowMoved;

        foreach (var row in Rows) row.Dispose();
    }
}
