using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Scripts.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Scripts.UI;



// which script's binding is open in the bottom panel editor (at most one, like a selected key or bind box)
public static class ScriptSelection
{
    private static ScriptModel? _selected;

    public static ScriptModel? Selected => _selected;

    public static event Action? Changed;



    // clicking the open bind box again closes it
    public static void Toggle(ScriptModel script) => Set(_selected == script ? null : script);



    public static void Set(ScriptModel? script)
    {
        if (_selected == script) return;

        _selected = script;
        Changed?.Invoke();
    }
}



// one script's row: its name box, then (once it has a name or a binding) a bind box like a dock button's (click to edit
// the binding below, drop a binding on it, edit buttons on hover, it flashes when it runs)
public partial class ScriptRowViewModel : ViewModelBase, IDroppable, IEditButtons
{
    // private



    private readonly ScriptModel _script;
    private readonly IDroppable _list; // the scripts, which reorder (see PassesOn)
    private BindableModel? _bindable;
    private readonly Flash _flash; // lights up IsTriggered when it runs

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _showBindBox;
    [ObservableProperty] private bool _isBound;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isTriggered;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _bindLabel; // the binding's label as a second line, "" for none



    // public



    public bool CanCollapse => false; // the collapse button is hidden
    public bool IsCollapsed => false;
    public bool CanCopy => true;
    [RelayCommand] private void OnCopyButtonPressed() { if (_bindable != null) BlockClipboard.CopyBindable(_bindable); } // the whole binding
    public bool CanToggle => true;



    // constructor



    public ScriptRowViewModel(ScriptModel script, IDroppable list)
    {
        _script = script;
        _list = list;
        _bindable = null;
        _flash = new(isOn => IsTriggered = isOn);

        _name = _script.Name;
        _bindLabel = "";
        _isSelected = ScriptSelection.Selected == _script;

        SetBindable(_script.Bindable);
        ShowBindBox = !_script.IsEmpty;

        _script.NameChanged += OnScriptNameChanged;
        _script.BindableChanged += OnScriptBindableChanged;
        ScriptSelection.Changed += OnSelectionChanged;
    }



    // public api



    public bool Represents(ScriptModel script) => script == _script;



    // ui events



    // typing in the box goes straight to the model (like a dock button's name)
    partial void OnNameChanged(string value) => _script.SetName(value ?? "");



    public void OnBindBoxClicked() => ScriptSelection.Toggle(_script);



    [RelayCommand] private void OnToggleButtonPressed() => _bindable?.SetIsDisabled(!_bindable.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => _script.Unbind();



    [RelayCommand] private void OnCollapseButtonPressed() { }



    // drag drop: a bind box takes a whole binding (from a key or another bind box) while it has none



    public bool CanAcceptDropPayload(object payload) =>
        payload is BindableDragPayload dragged && dragged.Bindable != _bindable && _script.Bindable == null;



    // another script's binding dropped here moves that whole script before this one (the list handles it)
    public bool PassesOn(object payload) => _list.CanAcceptDropPayload(payload);



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BindableDragPayload dragged) return;

        var copy = BindableModel.DeepCopy(dragged.Bindable);
        if (!_script.TryBind(copy))
        {
            copy.Dispose();
            return;
        }

        dragged.RemoveFromSource?.Invoke(); // only once the copy is safely bound
    }



    public object? GetDragPayload() => _bindable == null ? null : new BindableDragPayload(_bindable, _script.Unbind);



    // model events



    private void OnScriptNameChanged()
    {
        Name = _script.Name;
        ShowBindBox = !_script.IsEmpty;
    }



    private void OnScriptBindableChanged()
    {
        SetBindable(_script.Bindable);
        ShowBindBox = !_script.IsEmpty;
    }



    private void OnBindableChanged() => IsBound = _bindable?.HasBlock ?? false;



    private void OnBindableIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnBindableLabelChanged() => BindLabel = _bindable?.Label.Trim() ?? "";



    private void OnBindableTriggered() => _flash.Trigger();



    private void OnSelectionChanged() => IsSelected = ScriptSelection.Selected == _script;



    // helpers



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
        _script.NameChanged -= OnScriptNameChanged;
        _script.BindableChanged -= OnScriptBindableChanged;
        ScriptSelection.Changed -= OnSelectionChanged;
        SetBindable(null);
        _flash.Dispose();
    }
}
