using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// the rows of a Match Text block (shown through the ViewLocator as MatchTextRowListView)
public class MatchTextRowListViewModel : ViewModelBase
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
            _rows.Add(new(row, _model));

        _list.RowInserted += OnRowInserted;
        _list.RowRemoved += OnRowRemoved;
    }



    // model events



    private void OnRowInserted(MatchTextRow row, int index) => _rows.Insert(index, new(row, _model));



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

        foreach (var row in _rows)
            row.Dispose();
    }
}
