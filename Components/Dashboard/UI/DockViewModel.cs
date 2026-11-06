using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Dashboard.UI;



// one dock in the layout: its header (the edit buttons blocks and keys have, shown on hover; its name, applied when the
// box is left, a refused one puts the old one back; the kind's buttons) and its body. Another dock dropped on one of
// its sides moves there, a card from the dock list adds one there; the layout is then rebuilt from the model
public partial class DockViewModel : ViewModelBase, IEditButtons, ICommitOnLeave
{
    // private



    private readonly DashboardConfigModel _config;
    private readonly DockModel _dock;
    private readonly ViewModelBase _body;

    [ObservableProperty] private string _nameInput;
    [ObservableProperty] private bool _isDisabled; // red, nothing changes it



    // public



    public ViewModelBase Body => _body;
    public TimerDockViewModel? Timers => _body as TimerDockViewModel; // the header's buttons for all timers
    public ButtonDockViewModel? Buttons => _body as ButtonDockViewModel; // the header's press all
    public IndicatorDockViewModel? Indicators => _body as IndicatorDockViewModel; // the header's toggle all
    public bool IsConsole => _dock.Kind == DockKind.Console;
    public bool IsBlank => _dock.Kind == DockKind.Blank; // a grey box with no header, a copied dock is pasted into it
    public bool CanCollapse => false; // the collapse button is hidden
    public bool CanCopy => !IsBlank;
    public bool CanToggle => _dock.CanDisable;



    // constructor



    public DockViewModel(DashboardComponentModel model, DashboardConfigModel config, DockModel dock)
    {
        _config = config;
        _dock = dock;
        _body = dock.Kind switch
        {
            DockKind.Timer => new TimerDockViewModel(dock.Timers),
            DockKind.Button => new ButtonDockViewModel(dock),
            DockKind.Indicator => new IndicatorDockViewModel(dock),
            DockKind.Variable => new VariableDockViewModel(dock, GlobalContext.DataModel),
            DockKind.Blank => new BlankDockViewModel(config, dock),
            _ => new ConsoleDockViewModel(dock),
        };

        _nameInput = _dock.Name;
        _isDisabled = _dock.IsDisabled;

        _dock.NameChanged += OnDockNameChanged;
        _dock.IsDisabledChanged += OnDockIsDisabledChanged;
    }



    // drag drop



    public object? GetDragPayload() => new DockDragPayload(_dock);



    public static bool CanAcceptDrop(object payload) => payload is DockDragPayload or NewDockPayload;



    public void Drop(object payload, DockEdge edge)
    {
        if (payload is DockDragPayload dragged) _config.TryMoveDock(dragged.Dock, _dock, edge);
        else if (payload is NewDockPayload card) _config.TryAddDock(card.Kind, _dock, edge);
    }



    // ui events



    public void Commit()
    {
        if (!_config.TryRenameDock(_dock, NameInput)) NameInput = _dock.Name;
    }



    [RelayCommand] private void OnToggleButtonPressed() => _dock.SetIsDisabled(!_dock.IsDisabled);



    // the dock as it's saved (its kind, rows, on / off), pasted into a blank dock
    [RelayCommand] private void OnCopyButtonPressed() => BlockClipboard.Copy(DockPastePayload.Kind, _dock);



    [RelayCommand] private void OnRemoveButtonPressed() => _config.TryRemoveDock(_dock);



    [RelayCommand] private void OnCollapseButtonPressed() { }



    [RelayCommand] private void OnClearPressed() => _dock.ClearLines();



    // model events



    private void OnDockNameChanged() => NameInput = _dock.Name;



    private void OnDockIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    // disposal



    public override void Dispose()
    {
        _dock.NameChanged -= OnDockNameChanged;
        _dock.IsDisabledChanged -= OnDockIsDisabledChanged;
        _body.Dispose();
    }
}
