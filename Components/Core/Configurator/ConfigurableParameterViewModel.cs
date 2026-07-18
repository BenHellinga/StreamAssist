using System;
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



    // public



    public string Label => _label + ":";
    public ConfiguratorType Configurator => _configurator;
    public Type Type => _property.PropertyType;



    // constructor



    public ConfigurableParameterViewModel(ActionModel target, PropertyInfo property, ConfigurableParameterAttribute attribute)
    {
        _target = target;
        _property = property;
        _label = attribute.Label;
        _configurator = ResolveConfigurator(attribute.Editor, property.PropertyType);
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