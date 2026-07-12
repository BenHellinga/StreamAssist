using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Core;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.AppAction;



public partial class ActionListViewModel : ViewModelBase
{
    public ObservableCollection<IAppAction> Actions { get; } = [];



    public ActionListViewModel()
    {
        foreach (var action in ActionResolver.DiscoverAll().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            Actions.Add(action);
    }
}