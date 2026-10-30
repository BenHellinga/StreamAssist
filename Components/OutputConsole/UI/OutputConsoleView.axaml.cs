using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace StreamAssist.Components.OutputConsole.UI;



public partial class OutputConsoleView : UserControl
{
    // the small gap between messages: a line holding only an invisible character in a tiny font (a text block can't
    // space some line breaks differently from others). Every line is at least as tall as the text block's own font,
    // so the text block gets the tiny size and each message is put in a normal sized span. Copying leaves these out
    private const string Gap = "\u200B";
    private const double GapFontSize = 2.5; // about 3px tall
    private static readonly Regex GapLine = new(@"\r?\n\u200B(?=\r?\n)");

    private OutputConsoleViewModel? _viewModel;



    public OutputConsoleView()
    {
        InitializeComponent();

        LogText.FontSize = GapFontSize; // the messages themselves are normal sized, see ShowText

        AddHandler(SelectableTextBlock.CopyingToClipboardEvent, OnCopyingToClipboard);
    }



    // copies the selection without the gap lines, and hands it over to windows right away, otherwise it disappears
    // from the clipboard when the app closes
    private void OnCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not SelectableTextBlock text) return;

        e.Handled = true; // the copy is done here instead
        var copied = GapLine.Replace(text.SelectedText, "").Replace(Gap, "");

        Dispatcher.UIThread.Post(async () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;

            await clipboard.SetTextAsync(copied);
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

        ShowText();
    }



    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(OutputConsoleViewModel.Text)) return;

        ShowText();

        // auto scroll on new lines
        Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
    }



    // the log as runs: each line's "[time]" muted, the rest as normal, and a small gap between messages (it stays one
    // text, so selecting and copying across lines still works)
    private void ShowText()
    {
        var inlines = new InlineCollection();
        var muted = this.TryFindResource("TextMuted", ActualThemeVariant, out var brush) ? brush as IBrush : null;
        var size = this.TryFindResource("FontSizeNormal", ActualThemeVariant, out var found) && found is double normal ? normal : 14;
        var lines = _viewModel?.Text.Split('\n') ?? [];

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var stampEnd = line.StartsWith('[') ? line.IndexOf("] ", StringComparison.Ordinal) : -1;

            if (i > 0) inlines.Add(new LineBreak { FontSize = size });
            if (i > 0 && stampEnd >= 0)
            {
                // a new message (not the next line of one with line breaks in it): the gap line, in the text block's tiny size
                inlines.Add(new Run(Gap));
                inlines.Add(new LineBreak());
            }

            var span = new Span { FontSize = size };
            if (stampEnd < 0)
            {
                // e.g. the rest of a Console Message that has line breaks in it (an empty line keeps its height with a space)
                span.Inlines.Add(new Run(line == "" ? " " : line));
            }
            else
            {
                span.Inlines.Add(new Run(line[..(stampEnd + 1)]) { Foreground = muted });
                span.Inlines.Add(new Run(line[(stampEnd + 1)..]));
            }
            inlines.Add(span);
        }

        LogText.Inlines = inlines;
    }



    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewModel != null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;

        base.OnDetachedFromVisualTree(e);
    }
}
