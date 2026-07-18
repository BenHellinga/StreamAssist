using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;

namespace StreamAssist.AppActions;



public class PrintRandomNumberAction : ActionModel
{
    private static readonly Random _rng = new();

    [JsonIgnore] public override string Name => "Print Random Number";
    [JsonIgnore] public override string Description => "Prints a random number between 1 and 100 to the console.";



    public override Task SafeExecuteAsync()
    {
        AppLog.WriteLine($"Random: {_rng.Next(1, 101)}");
        return Task.CompletedTask;
    }
}