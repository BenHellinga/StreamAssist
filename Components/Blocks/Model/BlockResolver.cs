using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.Model;



// finds every block class through reflection, and converts blocks to/from their saved form
public static class BlockResolver
{
    private static readonly Dictionary<string, Type> _typeMap = BuildTypeMap();
    private static readonly Dictionary<Type, string> _nameMap = _typeMap.ToDictionary(kv => kv.Value, kv => kv.Key);



    private static Dictionary<string, Type> BuildTypeMap() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(BlockModel).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
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



    public static BlockModel? Resolve(PendingBlockData data)
    {
        if (!_typeMap.TryGetValue(data.TypeName, out var type))
        {
            AppLog.WriteWarning($"Could not load block '{data.TypeName}' - unknown block type, it was skipped");
            return null;
        }

        try
        {
            return (BlockModel?)data.Parameters.Deserialize(type);
        }
        catch (Exception ex)
        {
            AppLog.WriteError($"Could not load block '{data.TypeName}' - {ex.Message}");
            return null;
        }
    }



    public static PendingBlockData GetData(BlockModel block)
    {
        var type = block.GetType();
        var typeName = _nameMap.TryGetValue(type, out var name) ? name : type.Name;

        return new PendingBlockData
        {
            TypeName = typeName,
            Parameters = JsonSerializer.SerializeToElement(block, type)
        };
    }



    // copies a block and everything nested inside it
    public static BlockModel? DeepCopy(BlockModel block) => Resolve(GetData(block));



    // one fresh instance of every block type, used to fill the block list
    public static IReadOnlyList<BlockModel> DiscoverAll()
    {
        var result = new List<BlockModel>();

        foreach (var type in _typeMap.Values)
        {
            try
            {
                if (Activator.CreateInstance(type) is BlockModel block)
                    result.Add(block);
            }
            catch (Exception ex)
            {
                AppLog.WriteError($"Could not create block '{type.Name}' - {ex.Message}");
            }
        }

        return result;
    }
}
