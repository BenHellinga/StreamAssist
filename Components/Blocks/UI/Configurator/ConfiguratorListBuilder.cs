using System.Collections.ObjectModel;
using StreamAssist.Components.Blocks.Model;
using System.Linq;
using System.Reflection;

namespace StreamAssist.Components.Blocks.UI.Configurator;



public static class ConfiguratorListBuilder
{
    public static ObservableCollection<ConfigurableParameterViewModel> Build(BlockModel action)
    {
        var result = new ObservableCollection<ConfigurableParameterViewModel>();

        var props = action.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

        foreach (var prop in props)
        {
            var attr = prop.GetCustomAttribute<ConfigurableParameterAttribute>();
            if (attr is null) continue;
            result.Add(new ConfigurableParameterViewModel(action, prop, attr));
        }

        return result;
    }
}