using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;
using StreamAssist.Components.Core.Dragging;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.AppAction.UI;



public class ActionGroupViewModel : ViewModelBase, IDroppable
{
    // private



    private ActionGroupModel _model;
    private ObservableCollection<ActionViewModel> _actions;



    // public



    public ObservableCollection<ActionViewModel> Actions => _actions;



    // constructor



    public ActionGroupViewModel(ActionGroupModel model)
    {
        _model = model;

        _actions = [];
        foreach (var action in _model.Actions)
            InsertAction(action, _actions.Count);

        SubscribeToActionGroupModel(_model);
    }



    // drag drop



    public bool CanAcceptDropPayload(object payload) => payload is ActionDragPayload;



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not ActionDragPayload actionPayload) return;
        
        var index = zoneContext is ActionViewModel beforeItem ? _actions.IndexOf(beforeItem) : _actions.Count;
        if (index < 0) index = _actions.Count;

        var sourceIndex = _model.Actions.IndexOf(actionPayload.Action);
        var isSameList = sourceIndex >= 0;

        var copy = ActionModel.DeepCopy(actionPayload.Action);
        _model.TryInsertAction(copy, index);
        actionPayload.Callback?.Invoke();
    }



    // ui events



    private void OnRemoveActionRequested(ActionModel action) => _model.RemoveAction(action);



    // model events



    private void OnActionAdded(ActionModel action, int index) => InsertAction(action, index);



    private void OnActionRemoved(ActionModel action)
    {
        var viewModel = _actions.FirstOrDefault(a => a.RepresentsModel(action));
        if (viewModel == null) return;

        _actions.Remove(viewModel);
        UnsubscribeFromActionViewModel(viewModel);
        viewModel.Dispose();
    }



    // helpers



    private void InsertAction(ActionModel action, int index)
    {
        var viewModel = new ActionViewModel(action, () => _model.RemoveAction(action));
        _actions.Insert(index, viewModel);
        SubscribeToActionViewModel(viewModel);
    }



    private void SubscribeToActionGroupModel(ActionGroupModel model)
    {
        model.ActionAdded += OnActionAdded;
        model.ActionRemoved += OnActionRemoved;
    }



    private void UnsubscribeFromActionGroupModel(ActionGroupModel model)
    {
        model.ActionAdded -= OnActionAdded;
        model.ActionRemoved -= OnActionRemoved;
    }



    private void SubscribeToActionViewModel(ActionViewModel viewModel)
    {
        viewModel.RemoveActionRequested += OnRemoveActionRequested;
    }



    private void UnsubscribeFromActionViewModel(ActionViewModel viewModel)
    {
        viewModel.RemoveActionRequested -= OnRemoveActionRequested;
    }



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromActionGroupModel(_model);

        foreach (var action in _actions)
            UnsubscribeFromActionViewModel(action);
    }
}