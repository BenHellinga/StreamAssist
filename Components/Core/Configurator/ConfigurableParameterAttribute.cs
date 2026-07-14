using System;

namespace StreamAssist.Components.Core.Configurator;



public enum ConfiguratorType
{
    Auto, Text, FilePath, Number, Boolean 
}



[AttributeUsage(AttributeTargets.Property)]
public class ConfigurableParameterAttribute(string label, ConfiguratorType editor = ConfiguratorType.Auto) : Attribute
{
    public string Label { get; } = label;
    public ConfiguratorType Editor { get; } = editor;
}