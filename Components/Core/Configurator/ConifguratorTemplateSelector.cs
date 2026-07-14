using System;
using System.Globalization;
using Avalonia;
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
            ConfiguratorType.FilePath => "FilePathConfigurator",
            _                          => "TextConfigurator",
        };

        return Application.Current!.Resources[key] as DataTemplate;
    }



    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}