using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Blocks.UI.Configurator;



public partial class ConfigurableParameterViewModel : ObservableObject
{
    // private



    private readonly BlockModel _target;
    private readonly PropertyInfo _property;
    private readonly string _label;
    private readonly ConfiguratorType _configurator;

    private readonly IReadOnlyList<string> _menuItems;
    public double _min;
    public double _max;



    // public



    public string Label => _label + ":";
    public ConfiguratorType Configurator => _configurator;
    public Type Type => _property.PropertyType;

    public IReadOnlyList<string> MenuItems => _menuItems;
    public double Min => _min;
    public double Max => _max;



    // constructor



    public ConfigurableParameterViewModel(BlockModel target, PropertyInfo property, ConfigurableParameterAttribute attribute)
    {
        _target = target;
        _property = property;
        _label = attribute.Label;
        _configurator = ResolveConfigurator(attribute.Editor, property.PropertyType);

        _menuItems = _configurator == ConfiguratorType.Menu ? ResolveMenuItems(target, property) : [];
        (_min, _max) = _configurator == ConfiguratorType.Slider ? ResolveBounds(target, property) : (0, 1);
    }



    private static ConfiguratorType ResolveConfigurator(ConfiguratorType requested, Type propertyType)
    {
        if (requested != ConfiguratorType.Auto) return requested;

        if (propertyType == typeof(bool)) return ConfiguratorType.Boolean;
        if (propertyType == typeof(int)) return ConfiguratorType.Number;
        if (propertyType == typeof(float)) return ConfiguratorType.Number;
        if (propertyType == typeof(double)) return ConfiguratorType.Number;

        return ConfiguratorType.Text;
    }



    private static IReadOnlyList<string> ResolveMenuItems(BlockModel target, PropertyInfo property)
    {
        var methodName = $"Get{property.Name}MenuItems";

        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method is null) return ["null"];
        if (method.Invoke(target, null) is not List<string> list) return ["null"];
        if (list.Count <= 0) return ["null"];

        return list;
    }



    private static (double, double) ResolveBounds(BlockModel target, PropertyInfo property)
    {
        var methodName = $"Get{property.Name}Bounds";

        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (method is null) return (0, 1);
        if (method.Invoke(target, null) is not ValueTuple<double, double> bounds) return (0, 1);
        if (bounds.Item1 >= bounds.Item2) return (0, 1);

        return bounds;
    }



    public object? Value
    {
        get => _property.GetValue(_target);
        set
        {
            var converted = value is not null
                ? Convert.ChangeType(value, Nullable.GetUnderlyingType(_property.PropertyType) ?? _property.PropertyType)
                : value;

            _property.SetValue(_target, converted);
            OnPropertyChanged();
        }
    }



    // ui events



    [RelayCommand]
    private async Task OnBrowsePressed()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null) return;

        // opens in Assets/, and only a file inside it is taken (blocks can't reach anything else)
        AppPaths.EnsureAssetFolders();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Select {_label} (inside assets)",
            AllowMultiple = false,
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(AppPaths.AssetsDir),
        });

        if (files.Count == 0) return;

        if (!AppPaths.TryMakeAssetPath(files[0].Path.LocalPath, out var path))
        {
            AppLog.WriteError($"can't use '{files[0].Name}', it isn't in the assets folder");
            return;
        }

        Value = path;
    }



    private static TopLevel? GetTopLevel()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
}