using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamAssist.Core.Mvvm;



// one of the main window's tab buttons: its label, how to make its page, and whether it's the one shown (purple)
public partial class ViewModelTabItem(string label, Func<ViewModelBase> tabViewModelFactory) : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string Label { get; } = label;
    public Func<ViewModelBase> CreateTabViewModel { get; } = tabViewModelFactory;
}
