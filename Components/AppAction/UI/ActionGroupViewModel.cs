using System.Collections.ObjectModel;
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
            _actions.Add(new ActionViewModel(action));
    }
}