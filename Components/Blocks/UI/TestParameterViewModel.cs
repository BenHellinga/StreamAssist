using System;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// one row of the editor's Parameters list: a parameter the trigger passes (name, kind and what it is fixed, the value
// typed for Test), or an extra one with a typed name too (the growing rows under them, for a parameter the list
// doesn't know, e.g. one a script reads that another block sets)
public sealed partial class TestParameterViewModel : ViewModelBase
{
    // private



    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEmpty))] private string _name;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsEmpty))] private string _value;



    // public



    public bool IsExtra { get; }
    public string Kind { get; }
    public string Description { get; }
    public bool IsEmpty => Name.Trim() == "" && Value == "";



    // events



    public event Action? Edited; // the name or value was typed in



    // constructors



    public TestParameterViewModel(ParameterInfo parameter, string value)
    {
        _name = parameter.Name;
        _value = value;
        IsExtra = false;
        Kind = parameter.Kind.ToString();
        Description = parameter.Description;
    }



    public TestParameterViewModel(string name, string value)
    {
        _name = name;
        _value = value;
        IsExtra = true;
        Kind = "";
        Description = "";
    }



    // ui events



    partial void OnNameChanged(string value) => Edited?.Invoke();



    partial void OnValueChanged(string value) => Edited?.Invoke();



    // disposal (nothing held)



    public override void Dispose() { }
}
