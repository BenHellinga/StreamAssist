using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Core;
using StreamAssist.Components.Main.Model;

namespace StreamAssist.Components.Main.UI;



public partial class OutputConsoleViewModel : ViewModelBase
{
    public static ObservableCollection<string> Lines => AppLog.Lines;
    [RelayCommand] private static void Clear() => AppLog.Clear();
}
