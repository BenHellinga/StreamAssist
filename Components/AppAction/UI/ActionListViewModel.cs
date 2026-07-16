using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Core;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction.UI;



public partial class ActionListViewModel : ViewModelBase
{
    // private


    private ObservableCollection<ActionCardViewModel> _actions;



    // public



    public ObservableCollection<ActionCardViewModel> Actions => _actions;



    // constructor



    public ActionListViewModel()
    {
        _actions = [];
        foreach (var action in ActionResolver.DiscoverAll().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            _actions.Add(new(action));
    }



    // disposal



    public override void Dispose() {}
}