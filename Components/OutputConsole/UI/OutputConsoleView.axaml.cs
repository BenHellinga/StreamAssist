using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.OutputConsole.UI;



public partial class OutputConsoleView : UserControl
{
    public OutputConsoleView()
    {
        InitializeComponent();

        AppLog.Lines.CollectionChanged += OnLinesChanged;
    }



    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // auto scroll on new lines
        Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
    }
}
