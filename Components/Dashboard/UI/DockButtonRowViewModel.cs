using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Dashboard.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Dashboard.UI;



// one row of a button dock: its name box, then (once it has a name or a binding) a bind box like a redeem's (click to
// edit the binding below, drop a binding on it, edit buttons on hover), ▶ to press it, × to delete the row
public partial class DockButtonRowViewModel : ViewModelBase, IDroppable, IEditButtons
{
    // private



    private readonly DockModel _dock;
    private readonly DockButton _button;
    private readonly IDroppable _list; // the dock's rows, which reorder (see PassesOn)
    private BindableModel? _bindable;
    private readonly Flash _flash; // lights up IsTriggered when it runs

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _showBindBox;
    [ObservableProperty] private bool _isBound;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isTriggered;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _canPress; // bound, and the dock is on
    [ObservableProperty] private string _bindLabel; // the binding's label as a second line, "" for none



    // public



    public bool CanCollapse => false; // the collapse button is hidden
    public bool CanCopy => true;
    [RelayCommand] private void OnCopyButtonPressed() { if (_bindable != null) BlockClipboard.CopyBindable(_bindable); } // the whole binding, pastes on other keys / bind boxes
    public bool CanToggle => true;



    // constructor



    public DockButtonRowViewModel(DockModel dock, DockButton button, IDroppable list)
    {
        _dock = dock;
        _button = button;
        _list = list;
        _bindable = null;
        _flash = new(isOn => IsTriggered = isOn);

        _name = _button.Name;
        _bindLabel = "";
        _isSelected = DockButtonSelection.Selected == _button;

        SetBindable(_button.Bindable);
        UpdateShown();

        _button.NameChanged += OnButtonNameChanged;
        _button.BindableChanged += OnButtonBindableChanged;
        DockButtonSelection.Changed += OnSelectionChanged;
        _dock.IsDisabledChanged += OnDockIsDisabledChanged;
    }



    // public api



    public bool Represents(DockButton button) => button == _button;



    // ui events



    // typing in the box goes straight to the model (like a redeem's title)
    partial void OnNameChanged(string value) => _button.SetName(value ?? "");



    public void OnBindBoxClicked() => DockButtonSelection.Toggle(_button);



    [RelayCommand] private void OnPressPressed() => _dock.TryPressButton(_button);



    [RelayCommand] private void OnDeletePressed() => _dock.Buttons.Remove(_button);



    [RelayCommand] private void OnToggleButtonPressed() => _bindable?.SetIsDisabled(!_bindable.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => _button.Unbind();



    [RelayCommand] private void OnCollapseButtonPressed() { }



    // drag drop: a bind box takes a whole binding (from a key or another bind box) while it has none



    public bool CanAcceptDropPayload(object payload) =>
        payload is BindableDragPayload dragged && dragged.Bindable != _bindable && _button.Bindable == null;



    // another row's binding dropped here moves that whole row before this one (the dock's rows handle it)
    public bool PassesOn(object payload) => _list.CanAcceptDropPayload(payload);



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BindableDragPayload dragged) return;

        var copy = BindableModel.DeepCopy(dragged.Bindable);
        if (!_button.TryBind(copy))
        {
            copy.Dispose();
            return;
        }

        dragged.RemoveFromSource?.Invoke(); // only once the copy is safely bound
    }



    public object? GetDragPayload() => _bindable == null ? null : new BindableDragPayload(_bindable, _button.Unbind);



    // model events



    private void OnButtonNameChanged()
    {
        Name = _button.Name;
        UpdateShown();
    }



    private void OnButtonBindableChanged()
    {
        SetBindable(_button.Bindable);
        UpdateShown();
    }



    private void OnBindableChanged() => IsBound = _bindable?.HasBlock ?? false;



    private void OnBindableIsDisabledChanged(bool isDisabled)
    {
        IsDisabled = isDisabled;
        UpdateShown();
    }



    private void OnBindableLabelChanged() => BindLabel = _bindable?.Label.Trim() ?? "";



    private void OnBindableTriggered() => _flash.Trigger();



    private void OnSelectionChanged() => IsSelected = DockButtonSelection.Selected == _button;



    // helpers



    private void OnDockIsDisabledChanged(bool _) => UpdateShown();



    private void UpdateShown()
    {
        ShowBindBox = !_button.IsEmpty;
        CanPress = _bindable != null && !_bindable.IsDisabled && !_dock.IsDisabled;
    }



    private void SetBindable(BindableModel? bindable)
    {
        if (_bindable != null)
        {
            _bindable.Changed -= OnBindableChanged;
            _bindable.IsDisabledChanged -= OnBindableIsDisabledChanged;
            _bindable.LabelChanged -= OnBindableLabelChanged;
            _bindable.Triggered -= OnBindableTriggered;
        }

        _bindable = bindable;
        IsBound = _bindable?.HasBlock ?? false;
        IsDisabled = _bindable?.IsDisabled ?? false;
        BindLabel = _bindable?.Label.Trim() ?? "";

        if (_bindable != null)
        {
            _bindable.Changed += OnBindableChanged;
            _bindable.IsDisabledChanged += OnBindableIsDisabledChanged;
            _bindable.LabelChanged += OnBindableLabelChanged;
            _bindable.Triggered += OnBindableTriggered;
        }
    }



    // disposal



    public override void Dispose()
    {
        _button.NameChanged -= OnButtonNameChanged;
        _button.BindableChanged -= OnButtonBindableChanged;
        DockButtonSelection.Changed -= OnSelectionChanged;
        _dock.IsDisabledChanged -= OnDockIsDisabledChanged;
        SetBindable(null);
        _flash.Dispose();
    }
}
