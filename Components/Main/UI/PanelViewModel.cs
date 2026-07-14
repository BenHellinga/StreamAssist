using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Main.UI;



public partial class PanelViewModel : ViewModelBase
{
    private GridLength _oldWidth;
    private GridLength _oldHeight;
    private bool _isVisible;

    [ObservableProperty] private GridLength _width;
    [ObservableProperty] private GridLength _height; 
    [ObservableProperty] private double _minWidth;
    [ObservableProperty] private double _minHeight; 
    [ObservableProperty] private GridLength _splitterSize;

    [ObservableProperty] private ViewModelBase? _content;



    // constructor



    public PanelViewModel(int width = 200, int height = 200)
    {
        _oldWidth = new(width);
        _oldHeight = new(height);
        _isVisible = false;

        _width = new(0);
        _height = new(0);
        _minWidth = 0;
        _minHeight = 0;
        _splitterSize = new(0);

        _content = null;
    }



    // setters



    public void SetContent(ViewModelBase? content)
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
            Width = _oldWidth;
            Height = _oldHeight;
            MinWidth = 100;
            MinHeight = 100;
            SplitterSize = new(10);
        }
        else
        {
            _oldWidth = Width;
            _oldHeight = Height;

            Width = new(0);
            Height = new(0);
            MinWidth = 0;
            MinHeight = 0;
            SplitterSize = new(0);
        }
    }



    // disposal



    public override void Dispose()
    {
        Content?.Dispose();
    }
}