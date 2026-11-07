using System;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Background;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// one widget's box: its header (the name, applied when the box is left, a refused one puts the old one back; Spin for
// a wheel; its address copied or opened in a browser), the corner buttons blocks and docks have (copy, collapse,
// on / off, drag, remove), then its kind's settings and preview (Body)
public partial class WidgetViewModel : ViewModelBase, IEditButtons, ICommitOnLeave
{
    // private



    private readonly WidgetsComponentModel _model;
    private readonly WidgetsConfigModel _config;
    private readonly WidgetModel _widget;
    private readonly ViewModelBase _body;

    [ObservableProperty] private string _nameInput;
    [ObservableProperty] private bool _isDisabled; // red, nothing changes it
    [ObservableProperty] private bool _isCollapsed; // only its header shows



    // public



    public ViewModelBase Body => _body;
    public bool IsWheel => _widget is WheelWidget;
    public bool CanCollapse => true;
    public bool CanCopy => true;
    public bool CanToggle => true;



    // constructor



    public WidgetViewModel(WidgetsComponentModel model, WidgetsConfigModel config, WidgetModel widget, InputListModel? inputs)
    {
        _model = model;
        _config = config;
        _widget = widget;
        _body = widget switch
        {
            WheelWidget wheel => new WheelBodyViewModel(wheel),
            MicBounceWidget bounce => new MicBounceBodyViewModel(bounce, inputs),
            _ => throw new ArgumentException($"no view for widget kind {widget.Kind}"),
        };

        _nameInput = _widget.Name;
        _isDisabled = _widget.IsDisabled;
        _isCollapsed = _widget.IsCollapsed;

        SubscribeToWidget(_widget);
    }



    // public api



    public bool RepresentsModel(WidgetModel widget) => widget == _widget;



    // ui events



    public void Commit()
    {
        if (!_config.TryRenameWidget(_widget, NameInput)) NameInput = _widget.Name;
    }



    [RelayCommand]
    private void OnSpinPressed()
    {
        if (_widget is WheelWidget wheel && !wheel.TryStartSpin(null, out var error) && error != "") AppLog.WriteError($"can't spin, {error}");
    }



    // its address, for a browser source in OBS
    [RelayCommand] private void OnCopyUrlPressed() => _ = BlockClipboard.WriteText?.Invoke(WidgetManager.UrlOf(_model.Port, _widget.Name));



    // its page in the default browser (to look at it without OBS)
    [RelayCommand]
    private void OnOpenPressed()
    {
        try
        {
            Process.Start(new ProcessStartInfo(WidgetManager.UrlOf(_model.Port, _widget.Name)) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"can't open widget '{_widget.Name}' in a browser ({ex.Message})");
        }
    }



    [RelayCommand] private void OnToggleButtonPressed() => _widget.SetIsDisabled(!_widget.IsDisabled);



    [RelayCommand] private void OnCollapseButtonPressed() => _widget.SetIsCollapsed(!_widget.IsCollapsed);



    // the widget as it's saved (its kind first), pasted onto the list's drop zone
    [RelayCommand] private void OnCopyButtonPressed() => BlockClipboard.Copy(WidgetCopy.Kind, JsonSerializer.SerializeToElement<WidgetModel>(_widget));



    [RelayCommand] private void OnRemoveButtonPressed() => _config.TryRemoveWidget(_widget);



    // drag drop



    public object? GetDragPayload() => new WidgetDragPayload(_widget);



    // model events



    private void OnWidgetNameChanged() => NameInput = _widget.Name;



    private void OnWidgetIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnWidgetIsCollapsedChanged(bool isCollapsed) => IsCollapsed = isCollapsed;



    // helpers



    private void SubscribeToWidget(WidgetModel widget)
    {
        widget.NameChanged += OnWidgetNameChanged;
        widget.IsDisabledChanged += OnWidgetIsDisabledChanged;
        widget.IsCollapsedChanged += OnWidgetIsCollapsedChanged;
    }



    private void UnsubscribeFromWidget(WidgetModel widget)
    {
        widget.NameChanged -= OnWidgetNameChanged;
        widget.IsDisabledChanged -= OnWidgetIsDisabledChanged;
        widget.IsCollapsedChanged -= OnWidgetIsCollapsedChanged;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromWidget(_widget);
        _body.Dispose();
    }
}
