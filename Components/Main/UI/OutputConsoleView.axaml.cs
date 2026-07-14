using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Main.UI;



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
