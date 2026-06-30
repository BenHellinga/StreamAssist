using System;
using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Core;

namespace StreamAssist.Features.Keyboard;

/// <summary>
/// Drives the ActionList view — the scrollable panel of draggable actions
/// next to the keyboard. Discovers every IFeatureAction implementation in
/// the loaded assemblies via reflection, so new actions show up here with
/// no manual registration step.
/// </summary>
public partial class ActionListViewModel : ViewModelBase
{
    public ObservableCollection<IFeatureAction> Actions { get; } = [];

    public ActionListViewModel()
    {
        foreach (var action in ActionResolver.DiscoverAll().OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase))
            Actions.Add(action);
    }
}