using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Core.Mvvm;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.OutputConsole.UI;



public partial class OutputConsoleViewModel : ViewModelBase
{
    public static ObservableCollection<string> Lines => AppLog.Lines;
    [RelayCommand] private static void Clear() => AppLog.Clear();



    public override void Dispose()
    {
        
    }
}
