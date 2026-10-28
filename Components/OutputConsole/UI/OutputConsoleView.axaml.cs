using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace StreamAssist.Components.OutputConsole.UI;



public partial class OutputConsoleView : UserControl
{
    private OutputConsoleViewModel? _viewModel;



    public OutputConsoleView()
    {
        InitializeComponent();

        AddHandler(SelectableTextBlock.CopyingToClipboardEvent, OnCopyingToClipboard);
    }



    // hand copied text over to windows right away, otherwise it disappears from the clipboard when the app closes
    private void OnCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.FlushAsync();
        });
    }



    // follow the view model so new lines scroll the console to the bottom
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as OutputConsoleViewModel;
        if (_viewModel != null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }



    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // auto scroll on new lines
        if (e.PropertyName == nameof(OutputConsoleViewModel.Text))
            Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
    }



    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;

        base.OnDetachedFromVisualTree(e);
    }
}
