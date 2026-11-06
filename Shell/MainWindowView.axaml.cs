using Avalonia.Input.Platform;
using StreamAssist.Components.Blocks.UI;
using System.Threading.Tasks;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Shell;



public partial class MainWindowView : Window
{
    private readonly MainWindowViewModel _viewModel;
    private bool _isOpened;
    private bool _isApplyingLayout;



    public MainWindowView()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        AddHandler(PointerPressedEvent, OnBackgroundPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, OnTabDragOver, handledEventsToo: true);

        // copy / paste go through the system clipboard; something copied elsewhere shows once the app has focus again
        BlockClipboard.WriteText = text => Clipboard?.SetTextAsync(text) ?? Task.CompletedTask;
        BlockClipboard.ReadText = () => Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null);
        Activated += (_, _) => _ = BlockClipboard.RefreshAsync();

        // restore the saved window layout before the window is shown
        ApplyLayoutBounds();
        ApplyLayoutMode(_viewModel.WindowLayout.Mode);

        PositionChanged += OnWindowPositionChanged;
        _viewModel.WindowLayout.BoundsChanged += OnLayoutBoundsChanged;
        _viewModel.WindowLayout.ModeChanged += OnLayoutModeChanged;
    }



    private void OnBackgroundPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is InputElement { Focusable: true }) return;
        FocusManager?.Focus(null);
    }



    // something dragged over a tab button switches to that tab (so it can be dropped on its page)
    private void OnTabDragOver(object? sender, DragEventArgs e)
    {
        if (e.Source is not Visual source) return;
        if (source.FindAncestorOfType<Button>(includeSelf: true) is not { Classes: var classes, DataContext: ViewModelTabItem tab } || !classes.Contains("Tab")) return;

        if (tab != _viewModel.SelectedTab) _viewModel.SelectedTab = tab;
    }



    // window -> layout model



    private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e) => RecordBounds();



    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ClientSizeProperty) RecordBounds();
        else if (change.Property == WindowStateProperty) RecordMode();
    }



    // posted so the window state has settled first (maximizing moves/resizes the window before the state flips)
    private void RecordBounds()
    {
        if (!_isOpened || _isApplyingLayout) return;

        Dispatcher.UIThread.Post(() =>
        {
            // only the normal size is remembered, maximized/fullscreen are stored as the mode instead
            if (WindowState != WindowState.Normal) return;
            _viewModel.WindowLayout.SetBounds(Position.X, Position.Y, ClientSize.Width, ClientSize.Height);
        });
    }



    private void RecordMode()
    {
        if (!_isOpened || _isApplyingLayout) return;

        switch (WindowState)
        {
            case WindowState.Normal: _viewModel.WindowLayout.SetMode(WindowMode.Normal); break;
            case WindowState.Maximized: _viewModel.WindowLayout.SetMode(WindowMode.Maximized); break;
            case WindowState.FullScreen: _viewModel.WindowLayout.SetMode(WindowMode.FullScreen); break;
            // minimized isn't saved, the app reopens in the last visible state
        }
    }



    // layout model -> window



    private void OnLayoutBoundsChanged()
    {
        if (WindowState == WindowState.Normal) ApplyLayoutBounds();
    }



    private void OnLayoutModeChanged(WindowMode mode) => ApplyLayoutMode(mode);



    private void ApplyLayoutBounds()
    {
        var layout = _viewModel.WindowLayout;
        _isApplyingLayout = true;

        if (layout.HasSize)
        {
            Width = layout.Width;
            Height = layout.Height;
        }

        // only restore the position if it's still on a connected screen (e.g. a monitor was unplugged)
        if (layout.HasPosition)
        {
            var position = new PixelPoint((int)layout.Left, (int)layout.Top);
            var checkPoint = position + new PixelVector(50, 50);

            if (Screens.ScreenFromPoint(checkPoint) != null)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = position;
            }
        }

        _isApplyingLayout = false;
    }



    private void ApplyLayoutMode(WindowMode mode)
    {
        var state = mode switch
        {
            WindowMode.Maximized => WindowState.Maximized,
            WindowMode.FullScreen => WindowState.FullScreen,
            _ => WindowState.Normal
        };

        if (WindowState == state) return;

        _isApplyingLayout = true;
        WindowState = state;
        _isApplyingLayout = false;
    }



    // lifetime



    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _isOpened = true;

        // init keyboard hook from window handle
        nint hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero) _viewModel.KeyboardComponentManager.InitHwnd(hwnd);
    }



    protected override void OnClosed(EventArgs e)
    {

        PositionChanged -= OnWindowPositionChanged;
        _viewModel.WindowLayout.BoundsChanged -= OnLayoutBoundsChanged;
        _viewModel.WindowLayout.ModeChanged -= OnLayoutModeChanged;

        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
