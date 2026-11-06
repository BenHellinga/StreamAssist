using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// an indicator dock's body: its rows (the last one always empty, typing a name in it adds the next), each coloured by
// its state, which this dock keeps (Set Indicator names the dock and the row)
public sealed class IndicatorDockViewModel : ViewModelBase
{
    // private



    private readonly DockModel _dock;
    // public



    public ObservableCollection<IndicatorRowViewModel> Rows { get; }



    // constructor



    public IndicatorDockViewModel(DockModel dock)
    {
        _dock = dock;
        Rows = new(_dock.Indicators.Rows.Select(row => new IndicatorRowViewModel(row, _dock)));

        _dock.Indicators.RowInserted += OnRowInserted;
        _dock.Indicators.RowRemoved += OnRowRemoved;
    }



    // model events



    private void OnRowInserted(DockNameRow row, int index) => Rows.Insert(index, new IndicatorRowViewModel(row, _dock));



    private void OnRowRemoved(DockNameRow row)
    {
        var viewModel = Rows.FirstOrDefault(r => r.Represents(row));
        if (viewModel == null) return;

        Rows.Remove(viewModel);
        viewModel.Dispose();
    }



    // disposal



    public override void Dispose()
    {
        _dock.Indicators.RowInserted -= OnRowInserted;
        _dock.Indicators.RowRemoved -= OnRowRemoved;

        foreach (var row in Rows) row.Dispose();
    }
}



// one row: the name box, its text green while the indicator is on, red while off, grey while nothing has set it
public sealed partial class IndicatorRowViewModel : ViewModelBase
{
    // private



    private readonly DockNameRow _row;
    private readonly DockModel _dock; // keeps the states

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _isOn;
    [ObservableProperty] private bool _isOff;



    // constructor



    public IndicatorRowViewModel(DockNameRow row, DockModel dock)
    {
        _row = row;
        _dock = dock;
        _name = _row.Name;

        UpdateState();

        _row.NameChanged += OnRowNameChanged;
        _dock.IndicatorStateChanged += OnIndicatorStateChanged;
    }



    // public api



    public bool Represents(DockNameRow row) => row == _row;



    // ui events



    // typing in the box goes straight to the model
    partial void OnNameChanged(string value) => _row.SetName(value ?? "");



    // model events



    private void OnRowNameChanged()
    {
        Name = _row.Name;
        UpdateState();
    }



    // any indicator changed: cheap to just look this one up again
    private void OnIndicatorStateChanged(string _) => UpdateState();



    // helpers



    private void UpdateState()
    {
        var state = _row.IsEmpty ? null : _dock.IndicatorState(_row.Name);
        IsOn = state == true;
        IsOff = state == false;
    }



    // disposal



    public override void Dispose()
    {
        _row.NameChanged -= OnRowNameChanged;
        _dock.IndicatorStateChanged -= OnIndicatorStateChanged;
    }
}
