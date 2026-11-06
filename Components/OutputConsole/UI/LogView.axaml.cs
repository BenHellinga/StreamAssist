using System;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace StreamAssist.Components.OutputConsole.UI;



// see LogView.axaml: Text is the whole log, one line per message ("[time] message"), shown with the [time]s muted
public partial class LogView : UserControl
{
    // the small gap between messages: a line holding only an invisible character in a tiny font (a text block can't
    // space some line breaks differently from others). Every line is at least as tall as the text block's own font,
    // so the text block gets the tiny size and each message is put in a normal sized span. Copying leaves these out
    private const string Gap = "\u200B";
    private const double GapFontSize = 2.5; // about 3px tall
    private static readonly Regex GapLine = new(@"\r?\n\u200B(?=\r?\n)");

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<LogView, string>(nameof(Text), "");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }



    public LogView()
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



    // new lines: shown, and the log scrolled to the bottom
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty) return;

        ShowText();
        Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
    }



    // the log as runs: each line's "[time]" muted, the rest as normal, and a small gap between messages (it stays one
    // text, so selecting and copying across lines still works)
    private void ShowText()
    {
        var inlines = new InlineCollection();
        var muted = this.TryFindResource("TextMuted", ActualThemeVariant, out var brush) ? brush as IBrush : null;
        var size = this.TryFindResource("FontSizeNormal", ActualThemeVariant, out var found) && found is double normal ? normal : 14;
        var lines = Text.Split('\n');

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
}
