using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.StreamAction;



public partial class ActionListViewModel : ViewModelBase
{
    public ObservableCollection<IStreamAction> Actions { get; } = [];



    public ActionListViewModel()
    {
        foreach (var action in ActionResolver.DiscoverAll().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            Actions.Add(action);
    }
}