using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.AppAction.Model;

namespace StreamAssist.Components.Core.Configurator;



public partial class ConfigurableParameterViewModel : ObservableObject
{
    // private



    private readonly ActionModel _target;
    private readonly PropertyInfo _property;
    private readonly string _label;
    private readonly ConfiguratorType _configurator;
    private readonly IReadOnlyList<string> _menuItems;



    // public



    public string Label => _label + ":";
    public ConfiguratorType Configurator => _configurator;
    public IReadOnlyList<string> MenuItems => _menuItems;
    public Type Type => _property.PropertyType;



    // constructor



    public ConfigurableParameterViewModel(ActionModel target, PropertyInfo property, ConfigurableParameterAttribute attribute)
    {
        _target = target;
        _property = property;
        _label = attribute.Label;
        _configurator = ResolveConfigurator(attribute.Editor, property.PropertyType);
        _menuItems = _configurator == ConfiguratorType.Menu ? ResolveMenuItems(target, property) : [];
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



    private static IReadOnlyList<string> ResolveMenuItems(ActionModel target, PropertyInfo property)
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

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Select {_label}",
            AllowMultiple = false
        });

        if (files.Count == 0) return;

        Value = files[0].Path.LocalPath;
    }



    private static TopLevel? GetTopLevel()
        => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
}