using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Actions;



public class PrintTimestampAction : IAppAction
{
    [JsonIgnore] public string Name => "Print Timestamp";
    [JsonIgnore] public string Description => "Prints the current date and time to the console.";



    public Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]");
        return Task.CompletedTask;
    }
}