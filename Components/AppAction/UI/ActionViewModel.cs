using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Configurator;
using StreamAssist.Components.Core.Dragging;
using StreamAssist.Components.Keyboard.Model;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionViewModel : ViewModelBase, IDraggable
{
    // private



    private readonly ActionModel _action;
    private readonly Func<bool>? _removeFromSource;
    private readonly ObservableCollection<ConfigurableParameterViewModel> _configurators;



    // public



    public string Name => _action.Name;
    public string Description => _action.Description;
    public ActionModel Action => _action;
    public ObservableCollection<ConfigurableParameterViewModel> Configurators => _configurators;



    // events



    public event Action<ActionModel>? RemoveActionRequested;



    // constructor



    public ActionViewModel(ActionModel action, Func<bool>? removeFromSource = null)
    {
        _action = action;
        _removeFromSource = removeFromSource;
        _configurators = ConfiguratorBuilder.Build(action);
    }



    // ui events



    [RelayCommand]
    private void OnRemoveActionRequested() => RemoveActionRequested?.Invoke(_action);



    // drag drop



    public object? GetDragPayload() => new ActionDragPayload(_action, _removeFromSource);



    // public api



    public bool RepresentsModel(ActionModel action) => action == _action;



    // diposal



    public override void Dispose() {}
}