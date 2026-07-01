using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;



public partial class ActionListViewModel : ViewModelBase
{
    public ObservableCollection<IFeatureAction> Actions { get; } = [];



    public ActionListViewModel()
    {
        foreach (var action in ActionResolver.DiscoverAll().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            Actions.Add(action);
    }
}