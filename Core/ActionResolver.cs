using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StreamAssist.Core;

/// <summary>
/// Maps IFeatureAction instances to/from a stable string key (the action's
/// full type name) for persistence, and discovers all available action types
/// via reflection. Actions are expected to have a public parameterless
/// constructor and currently carry no per-binding state — when ActionPanel
/// grows argument support, this is the place that'll need to change.
/// </summary>
public static class ActionResolver
{
    /// <summary>The key to persist for a given bound action.</summary>
    public static string GetKey(IFeatureAction action) => action.GetType().FullName!;

    /// <summary>Re-creates an action instance from a key saved via GetKey.
    /// Null if the type can't be found (e.g. a renamed/removed action) —
    /// callers should treat that the same as "unbound" rather than throwing.</summary>
    public static IFeatureAction? Resolve(string typeName)
    {
        var type = DiscoverActionTypes().FirstOrDefault(t => t.FullName == typeName);
        return type is null ? null : Activator.CreateInstance(type) as IFeatureAction;
    }

    /// <summary>Creates one instance of every discovered action type.</summary>
    public static IEnumerable<IFeatureAction> DiscoverAll()
    {
        foreach (var type in DiscoverActionTypes())
            if (Activator.CreateInstance(type) is IFeatureAction action)
                yield return action;
    }

    private static IEnumerable<Type> DiscoverActionTypes()
    {
        var actionType = typeof(IFeatureAction);

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t is { IsInterface: false, IsAbstract: false } && actionType.IsAssignableFrom(t));
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