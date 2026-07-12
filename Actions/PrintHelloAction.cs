using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.Actions;



public class PrintRandomNumberAction : IAppAction
{
    private static readonly Random _rng = new();

    [JsonIgnore] public string Name => "Print Random Number";
    [JsonIgnore] public string Description => "Prints a random number between 1 and 100 to the console.";



    public Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"Random: {_rng.Next(1, 101)}");
        return Task.CompletedTask;
    }
}