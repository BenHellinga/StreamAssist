using System;
using System.Threading.Tasks;
using StreamAssist.Components.Main;

namespace StreamAssist.Components.StreamAction;



public class PrintHelloAction : IStreamAction
{
    public string Name        => "Print Hello";
    public string Description => "Prints 'Hello, StreamAssist!' to the console.";
    public Task SafeExecuteAsync() { AppLog.WriteLine("Hello, StreamAssist!"); return Task.CompletedTask; }
}



public class PrintTimestampAction : IStreamAction
{
    public string Name        => "Print Timestamp";
    public string Description => "Prints the current date and time to the console.";
    public Task SafeExecuteAsync() { AppLog.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]"); return Task.CompletedTask; }
}



public class PrintRandomNumberAction : IStreamAction
{
    private static readonly Random _rng = new();
    public string Name        => "Print Random Number";
    public string Description => "Prints a random number between 1 and 100 to the console.";
    public Task SafeExecuteAsync() { AppLog.WriteLine($"Random: {_rng.Next(1, 101)}"); return Task.CompletedTask; }
}