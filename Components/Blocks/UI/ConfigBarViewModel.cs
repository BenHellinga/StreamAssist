using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Blocks.UI;



// a tab's config picker: the configs dropdown (picking one switches the whole tab to it), a name box with ＋ (a copy of
// the current config under that name) and － (deletes the current config, pressed twice). Changes only go through the
// model's ConfigSet, and the bar follows it, so a switch made anywhere else shows here too
public partial class ConfigBarViewModel : ViewModelBase
{
    // private



    private readonly IConfigSet _configs;
    private bool _isRefreshing; // the bar is showing the model, not the user picking

    [ObservableProperty] private string? _selectedName;
    [ObservableProperty] private string _newName;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DeleteText))] private bool _isDeleteArmed;
    [ObservableProperty] private bool _hasConfig; // － only shows while there's one to delete



    // public



    public ObservableCollection<string> Names { get; }
    public string DeleteText => IsDeleteArmed ? "Delete?" : "－";



    // constructor



    public ConfigBarViewModel(IConfigSet configs)
    {
        _configs = configs;
        _isRefreshing = false;
        _selectedName = null;
        _newName = "";
        _isDeleteArmed = false;
        _hasConfig = false;
        Names = [];

        Refresh();
        _configs.ListChanged += Refresh;
    }



    // ui events



    partial void OnSelectedNameChanged(string? value)
    {
        if (_isRefreshing || value == null || value == _configs.SelectedName) return;
        _configs.TrySelect(value);
    }



    [RelayCommand] private void OnCreatePressed()
    {
        if (_configs.TryCreate(NewName)) NewName = "";
    }



    // the first press arms it, the second deletes the current config
    [RelayCommand] private void OnDeletePressed()
    {
        if (_configs.SelectedName is not { } name) return;

        if (!IsDeleteArmed) IsDeleteArmed = true;
        else _configs.TryDelete(name);
    }



    // model events



    private void Refresh()
    {
        _isRefreshing = true;

        Names.Clear();
        foreach (var name in _configs.Names) Names.Add(name);
        SelectedName = _configs.SelectedName;
        HasConfig = _configs.SelectedName != null;
        IsDeleteArmed = false;

        _isRefreshing = false;
    }



    // disposal



    public override void Dispose() => _configs.ListChanged -= Refresh;
}
