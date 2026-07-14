using System;

namespace StreamAssist.Components.Core;



public class ViewModelTabItem(string label, Func<IDisposable> tabViewModelFactory)
{
    public string Label { get; } = label;
    public Func<IDisposable> CreateTabViewModel { get; } = tabViewModelFactory;
}