using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml.Templates;

namespace StreamAssist.Components.Core.Configurator;



public class ConfiguratorTemplateSelector : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ConfiguratorType.Boolean  => "BoolConfigurator",
            ConfiguratorType.Number   => "NumberConfigurator",
            ConfiguratorType.Filepath => "FilepathConfigurator",
            ConfiguratorType.Menu     => "MenuConfigurator",
            _                          => "TextConfigurator",
        };

    return Application.Current!.TryFindResource(key, out var resource)
        ? resource as DataTemplate
        : null;
    }



    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}