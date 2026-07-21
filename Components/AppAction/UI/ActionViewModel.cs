using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Configurator;
using StreamAssist.Components.Core.Dragging;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionViewModel : ViewModelBase, IActionUIButtons
{
    // private



    private readonly ActionModel _action;
    private readonly Action? _removeFromSource;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;
    private IDisposable? _triggerTimer;

    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isTriggered;



    // public



    public string Name => _action.Name;
    public string Description => _action.Description;
    public ActionModel Action => _action;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;



    // events



    public event Action<ActionModel>? RemoveActionRequested;



    // constructor



    public ActionViewModel(ActionModel action, Action? removeFromSource)
    {
        _action = action;
        _removeFromSource = removeFromSource;
        _configurators = ConfiguratorListBuilder.Build(action);

        _isDisabled = _action.IsDisabled;

        SubscribeToActionModel(_action);
    }



    // ui events



    [RelayCommand] private void OnToggleButtonPressed() => _action.SetDisabled(!_action.IsDisabled);



    [RelayCommand] private void OnRemoveButtonPressed() => RemoveActionRequested?.Invoke(_action);



    // drag drop



    public object? GetDragPayload() => new ActionDragPayload(_action, _removeFromSource);



    // model events



    public void OnDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;



    public void OnTriggered() => FlashTriggered();



    // public api



    public bool RepresentsModel(ActionModel action) => action == _action;



    // helpers



    private void SubscribeToActionModel(ActionModel action)
    {
        action.IsDisabledChanged += OnDisabledChanged;
        action.Triggered += OnTriggered;
    }



    private void UnsubscribeFromActionModel(ActionModel action)
    {
        action.IsDisabledChanged -= OnDisabledChanged;
        action.Triggered -= OnTriggered;
    }



    private void FlashTriggered()
    {
        IsTriggered = true;

        _triggerTimer?.Dispose();
        _triggerTimer = DispatcherTimer.RunOnce(() =>
        {
            _triggerTimer = null;
            IsTriggered = false;
        }, TimeSpan.FromMilliseconds(50));
    }



    // diposal



    public override void Dispose()
    {
        UnsubscribeFromActionModel(_action);
        _triggerTimer?.Dispose();
    }
}