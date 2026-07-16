using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using StreamAssist.Components.AppAction.Model;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.AppAction.UI;



public class ActionGroupViewModel : ViewModelBase
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
            _actions.Add(new(action));

        SubscribeToActionGroupModel(_model);
    }



    // ui events



    // model events



    private void OnActionAdded(IAppAction action, int index) => _actions.Insert(index, new(action));



    private void OnActionRemoved(IAppAction action)
    {
        var viewModel = _actions.FirstOrDefault(a => a.RepresentsModel(action));
        if (viewModel == null) return;
        _actions.Remove(viewModel);
        viewModel.Dispose();
    }



    // helper



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



    // disposal



    public override void Dispose()
    {
        UnsubscribeFromActionGroupModel(_model);
    }
}