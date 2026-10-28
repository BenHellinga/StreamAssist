using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.OutputConsole.UI;



public partial class OutputConsoleViewModel : ViewModelBase
{
    // private



    // the whole log as one text, so it can be selected and copied across lines
    [ObservableProperty] private string _text;



    // constructor



    public OutputConsoleViewModel()
    {
        _text = string.Join('\n', AppLog.Lines);

        AppLog.Lines.CollectionChanged += OnLinesChanged;
    }



    // ui events



    [RelayCommand] private static void Clear() => AppLog.Clear();



    // model events



    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Text = string.Join('\n', AppLog.Lines);



    // disposal



    public override void Dispose()
    {
        AppLog.Lines.CollectionChanged -= OnLinesChanged;
    }
}
