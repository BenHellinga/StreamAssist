using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// the rows of a Match Text block (shown through the ViewLocator as MatchTextRowListView). Dragging a row's binding or
// block onto another row moves the whole row before that one
public class MatchTextRowListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly MatchTextRowList _list;
    private readonly ChatbotComponentModel _model; // the rows' bind boxes show their command
    private readonly ObservableCollection<MatchTextRowViewModel> _rows;



    // public



    public ObservableCollection<MatchTextRowViewModel> Rows => _rows;



    // constructor



    public MatchTextRowListViewModel(MatchTextRowList list, ChatbotComponentModel model)
    {
        _list = list;
        _model = model;
        _rows = [];

        foreach (var row in _list.Rows)
            _rows.Add(new(row, _model, this));

        _list.RowInserted += OnRowInserted;
        _list.RowRemoved += OnRowRemoved;
        _list.RowMoved += OnRowMoved;
    }



    // drag drop



    // only reordering: the binding or block of one of these rows
    public bool CanAcceptDropPayload(object payload) => FindRow(payload) != null;



    // anything else goes on to the target around (e.g. inserting before the block it's in)
    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindRow(payload) is not { } row) return;
        if (zoneContext is not MatchTextRowViewModel before) return;

        _list.TryMoveRow(row, _rows.IndexOf(before));
    }



    private MatchTextRow? FindRow(object payload) => payload switch
    {
        BindableDragPayload dragged => _list.Rows.FirstOrDefault(r => r.Target.Bindable == dragged.Bindable),
        BlockDragPayload dragged => _list.Rows.FirstOrDefault(r => r.Target.Child.Block == dragged.Block),
        _ => null,
    };



    // model events



    private void OnRowInserted(MatchTextRow row, int index) => _rows.Insert(index, new(row, _model, this));



    private void OnRowMoved(MatchTextRow _, int from, int to) => _rows.Move(from, to);



    private void OnRowRemoved(MatchTextRow row)
    {
        var viewModel = _rows.FirstOrDefault(r => r.RepresentsModel(row));
        if (viewModel == null) return;

        _rows.Remove(viewModel);
        viewModel.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        _list.RowInserted -= OnRowInserted;
        _list.RowRemoved -= OnRowRemoved;
        _list.RowMoved -= OnRowMoved;

        foreach (var row in _rows)
            row.Dispose();
    }
}
