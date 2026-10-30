using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Chatbot.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Chatbot.UI;



// what comes after a row / Parameter: the chat block slot and a bind box (works like a key, shows the command that reaches it)
// filling one hides the other, so only one is ever filled
public partial class ChatTargetViewModel : ViewModelBase, IDroppable, IEditButtons
{
    // private



    private readonly ChatTarget _target;
    private readonly ChatbotComponentModel _model;
    private readonly BlockSlotViewModel _child;
    private BindableModel? _bindable;

    [ObservableProperty] private string _bindLabel; // the binding's label as a second line, "" for none
    private readonly Flash _flash; // lights up IsTriggered when it runs

    [ObservableProperty] private bool _isBound;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isTriggered;
    [ObservableProperty] private bool _showBindBox;
    [ObservableProperty] private bool _showSlot;
    [ObservableProperty] private string _command; // e.g. "!pay {user} {amount}", a redeem's title or "Raid"



    // public



    public BlockSlotViewModel Child => _child;



    // constructor



    public ChatTargetViewModel(ChatTarget target, ChatbotComponentModel model)
    {
        _target = target;
        _model = model;
        _child = new(_target.Child);
        _bindable = null;
        _bindLabel = "";
        _flash = new(isOn => IsTriggered = isOn);

        _isSelected = ChatTargetSelection.Selected == _target;
        _isTriggered = false;
        _command = "";

        SetBindable(_target.Bindable);
        UpdateShown();
        UpdateCommand();

        _model.Changed += OnModelChanged;
        _target.BindableChanged += OnTargetBindableChanged;
        _target.ContentsChanged += OnTargetContentsChanged;
        ChatTargetSelection.Changed += OnSelectionChanged;
    }



    // ui events



    public void OnBindBoxClicked() => ChatTargetSelection.Toggle(_target);



    [RelayCommand] private void OnToggleButtonPressed() => _bindable?.SetIsDisabled(!_bindable.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => _target.Unbind();



    // bind boxes can't collapse (the button is hidden)
    public bool CanCollapse => false;
    [RelayCommand] private void OnCollapseButtonPressed() { }



    // drag drop



    // a bind box is a binding slot like a key: it takes whole bindings, and only when nothing is after the row yet
    public bool CanAcceptDropPayload(object payload) =>
        payload is BindableDragPayload bindablePayload
        && bindablePayload.Bindable != _bindable
        && _target.IsEmpty;



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BindableDragPayload bindablePayload) return;

        var copy = BindableModel.DeepCopy(bindablePayload.Bindable);
        if (!_target.TryBind(copy))
        {
            copy.Dispose();
            return;
        }

        // only remove the original once the copy is safely bound
        bindablePayload.RemoveFromSource?.Invoke();
    }



    public object? GetDragPayload()
    {
        if (_bindable == null) return null;

        var target = _target;
        return new BindableDragPayload(_bindable, target.Unbind);
    }



    // model events



    private void OnTargetBindableChanged() => SetBindable(_target.Bindable);



    private void OnTargetContentsChanged() => UpdateShown();



    // a pattern or parameter name on the way here may have changed
    private void OnModelChanged() => UpdateCommand();



    private void OnBindableChanged() => IsBound = _bindable?.HasBlock ?? false;



    private void OnBindableIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    private void OnBindableLabelChanged() => BindLabel = _bindable?.Label.Trim() ?? "";



    // bindings run off the ui thread
    private void OnBindableTriggered() => _flash.Trigger();



    private void OnSelectionChanged() => IsSelected = ChatTargetSelection.Selected == _target;



    // helpers



    private void SetBindable(BindableModel? bindable)
    {
        UnsubscribeFromBindable(_bindable);

        _bindable = bindable;
        IsBound = _bindable?.HasBlock ?? false;
        IsDisabled = _bindable?.IsDisabled ?? false;
        BindLabel = _bindable?.Label.Trim() ?? "";

        SubscribeToBindable(_bindable);
    }



    private void UpdateShown()
    {
        ShowBindBox = _target.Child.Block == null;
        ShowSlot = _target.Child.Block != null || (_target.AllowsBlocks && !_target.EndsHere && _target.Bindable == null);
    }



    // the command that reaches it, or the redeem's title / the event's name
    private void UpdateCommand() => Command = _model.Describe(_target)?.Text ?? "";



    private void SubscribeToBindable(BindableModel? bindable)
    {
        bindable?.Changed += OnBindableChanged;
        bindable?.IsDisabledChanged += OnBindableIsDisabledChanged;
        bindable?.LabelChanged += OnBindableLabelChanged;
        bindable?.Triggered += OnBindableTriggered;
    }



    private void UnsubscribeFromBindable(BindableModel? bindable)
    {
        bindable?.Changed -= OnBindableChanged;
        bindable?.IsDisabledChanged -= OnBindableIsDisabledChanged;
        bindable?.LabelChanged -= OnBindableLabelChanged;
        bindable?.Triggered -= OnBindableTriggered;
    }



    // disposal



    public override void Dispose()
    {
        _model.Changed -= OnModelChanged;
        _target.BindableChanged -= OnTargetBindableChanged;
        _target.ContentsChanged -= OnTargetContentsChanged;
        ChatTargetSelection.Changed -= OnSelectionChanged;
        UnsubscribeFromBindable(_bindable);

        _flash.Dispose();
        _child.Dispose();
    }
}
