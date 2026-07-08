using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;

namespace StreamAssist.Components.Main;



public partial class OutputConsole : UserControl
{
    public OutputConsole()
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
