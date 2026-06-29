using System;
using System.Threading.Tasks;
using StreamAssist.Core.Models;

namespace StreamAssist.Core;



public class PrintHelloAction : IFeatureAction
{
    public string Name        => "Print Hello";
    public string Description => "Prints 'Hello, StreamAssist!' to the console.";
    public Task ExecuteAsync() { Console.WriteLine("Hello, StreamAssist!"); return Task.CompletedTask; }
}



public class PrintTimestampAction : IFeatureAction
{
    public string Name        => "Print Timestamp";
    public string Description => "Prints the current date and time to the console.";
    public Task ExecuteAsync() { Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]"); return Task.CompletedTask; }
}



public class PrintRandomNumberAction : IFeatureAction
{
    private static readonly Random _rng = new();
    public string Name        => "Print Random Number";
    public string Description => "Prints a random number between 1 and 100 to the console.";
    public Task ExecuteAsync() { Console.WriteLine($"Random: {_rng.Next(1, 101)}"); return Task.CompletedTask; }
}