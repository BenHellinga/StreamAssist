using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// a console dock's body: its lines as one text, drawn like the app console (LogView)
public partial class ConsoleDockViewModel : ViewModelBase
{
    // private



    private readonly DockModel _dock;

    [ObservableProperty] private string _text;



    // constructor



    public ConsoleDockViewModel(DockModel dock)
    {
        _dock = dock;
        _text = string.Join('\n', _dock.Lines);

        _dock.Lines.CollectionChanged += OnLinesChanged;
    }



    // model events



    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Text = string.Join('\n', _dock.Lines);



    // disposal



    public override void Dispose() => _dock.Lines.CollectionChanged -= OnLinesChanged;
}
