using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.AppActions;



public class PrintTimestampAction : AppActionBase
{
    [JsonIgnore] public override string Name => "Print Timestamp";
    [JsonIgnore] public override string Description => "Prints the current date and time to the console.";



    public override Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
        return Task.CompletedTask;
    }
}