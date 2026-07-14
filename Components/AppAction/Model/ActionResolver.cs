using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.AppAction.Model;



public static class ActionResolver
{
    private static readonly Dictionary<string, Type> _typeMap = BuildTypeMap();
    private static readonly Dictionary<Type, string> _nameMap = _typeMap.ToDictionary(kv => kv.Value, kv => kv.Key);



    private static Dictionary<string, Type> BuildTypeMap() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(IAppAction).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToDictionary(t => t.Name, t => t);



    private static IEnumerable<Type> SafeGetTypes(Assembly a)
    {
        try
        {
            return a.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t is not null)!;
        }
    }



    public static IAppAction? Resolve(PendingActionData data)
    {
        if (!_typeMap.TryGetValue(data.TypeName, out var type))
        {
            AppLog.WriteWarning($"Unknown action type '{data.TypeName}' - binding skipped");
            return null;
        }

        try
        {
            return (IAppAction?)data.Parameters.Deserialize(type);
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"Failed to load action '{data.TypeName}': {ex.Message}");
            return null;
        }
    }



    public static PendingActionData GetKey(IAppAction action)
    {
        var type = action.GetType();
        var typeName = _nameMap.TryGetValue(type, out var name) ? name : type.Name;
        
        return new PendingActionData
        {
            TypeName = typeName,
            Parameters = JsonSerializer.SerializeToElement(action, type)
        };
    }



    public static IReadOnlyList<IAppAction> DiscoverAll()
    {
        var result = new List<IAppAction>();

        foreach (var type in _typeMap.Values)
        {
            try
            {
                if (Activator.CreateInstance(type) is IAppAction action)
                    result.Add(action);
                else
                    AppLog.WriteWarning($"Action type '{type.Name}' could not be instantiated as IAppAction");
            }
            catch (Exception ex)
            {
                AppLog.WriteError($"Failed to create instance of action '{type.Name}': {ex.Message}");
            }
        }

        return result;
    }
}