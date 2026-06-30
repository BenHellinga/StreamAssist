using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
        foreach (var action in DiscoverActions())
            Actions.Add(action);
    }



    private static IEnumerable<IFeatureAction> DiscoverActions()
    {
        var actionType = typeof(IFeatureAction);

        var candidateTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsInterface: false, IsAbstract: false } && actionType.IsAssignableFrom(t));

        var actions = new List<IFeatureAction>();
        foreach (var type in candidateTypes)
        {
            // Actions are expected to have a public parameterless constructor.
            if (Activator.CreateInstance(type) is IFeatureAction action)
                actions.Add(action);
        }

        return actions.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase);
    }



    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Some assemblies (esp. third-party) fail to fully load every type;
            // just take what did load rather than blowing up discovery entirely.
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
