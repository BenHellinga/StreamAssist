using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Shell;



public partial class PanelViewModel : ViewModelBase
{
    private readonly WindowLayoutModel _layout;
    private readonly string _layoutKey;

    private GridLength _oldLength;
    private bool _isVisible;

    [ObservableProperty] private GridLength _length;
    [ObservableProperty] private double _minLength;
    [ObservableProperty] private GridLength _splitterLength;
    [ObservableProperty] private double _splitterMinLength;

    [ObservableProperty] private ViewModelBase? _content;



    // constructor



    // layoutKey is the name this panel's size is saved under in the window layout
    public PanelViewModel(WindowLayoutModel layout, string layoutKey, double defaultLength = 200)
    {
        _layout = layout;
        _layoutKey = layoutKey;

        _oldLength = new(_layout.GetPanelSize(_layoutKey, defaultLength));
        _isVisible = false;

        _length = new(0);
        _minLength = 0;
        _splitterLength = new(0);
        _splitterMinLength = 0;

        _content = null;

        _layout.PanelSizeChanged += OnLayoutPanelSizeChanged;
    }



    // setters



    public void TransferContent(ViewModelBase? content)
    {
        if (content != null ^ _isVisible)
            SetIsVisibile(!_isVisible);

        ViewModelBase? oldContent = Content;
        Content = content;
        oldContent?.Dispose();
    }



    public void SetIsVisibile(bool isVisible)
    {
        _isVisible = isVisible;

        if (isVisible)
        {
            Length = _oldLength;
            MinLength = 100;
            SplitterLength = new(15); // gap, 5px splitter, gap (Spacing.axaml SplitterLength)
            SplitterMinLength = 5;
        }
        else
        {
            _oldLength = Length;

            Length = new(0);
            MinLength = 0;
            SplitterLength = new(0);
            SplitterMinLength = 0;
        }
    }



    // ui events



    // the splitter was dragged (Length is bound two-way to the grid column/row)
    partial void OnLengthChanged(GridLength value)
    {
        if (_isVisible && value.IsAbsolute) _layout.SetPanelSize(_layoutKey, value.Value);
    }



    // model events



    // something else (e.g. an action) changed this panel's size
    private void OnLayoutPanelSizeChanged(string panel, double size)
    {
        if (panel != _layoutKey) return;

        if (_isVisible) Length = new(size);
        else _oldLength = new(size);
    }



    // disposal



    public override void Dispose()
    {
        _layout.PanelSizeChanged -= OnLayoutPanelSizeChanged;
        Content?.Dispose();
    }
}
