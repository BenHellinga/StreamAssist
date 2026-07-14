using System;

namespace StreamAssist.Components.Main.UI;



public class ComponentTabItem(string label, Func<IDisposable> activator)
{
    public string Label { get; } = label;
    public Func<IDisposable> Activate { get; } = activator;
}