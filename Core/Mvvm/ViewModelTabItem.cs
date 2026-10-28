using System;

namespace StreamAssist.Core.Mvvm;



public class ViewModelTabItem(string label, Func<ViewModelBase> tabViewModelFactory)
{
    public string Label { get; } = label;
    public Func<ViewModelBase> CreateTabViewModel { get; } = tabViewModelFactory;
}