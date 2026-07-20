using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Main.UI;



public partial class PanelViewModel : ViewModelBase
{
    private GridLength _oldLength;
    private bool _isVisible;

    [ObservableProperty] private GridLength _length;
    [ObservableProperty] private double _minLength;
    [ObservableProperty] private GridLength _splitterLength;
    [ObservableProperty] private double _splitterMinLength;

    [ObservableProperty] private ViewModelBase? _content;



    // constructor



    public PanelViewModel(int length = 200)
    {
        _oldLength = new(length);
        _isVisible = false;

        _length = new(0);
        _minLength = 0;
        _splitterLength = new(0);
        _splitterMinLength = 0;

        _content = null;
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
            SplitterLength = new(10);
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



    // disposal



    public override void Dispose()
    {
        Content?.Dispose();
    }
}