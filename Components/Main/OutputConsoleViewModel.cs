using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;

namespace StreamAssist.Components.Main;



public partial class OutputConsoleViewModel : ViewModelBase
{
    public ObservableCollection<string> Lines => AppLog.Lines;



    [RelayCommand]
    private void Clear() => AppLog.Clear();
}
