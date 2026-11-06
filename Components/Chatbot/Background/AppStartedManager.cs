using System;
using System.Collections.Generic;
using Avalonia.Threading;
using StreamAssist.Components.Chatbot.Model;

namespace StreamAssist.Components.Chatbot.Background;



// runs the App Started event once per launch: after the startup connection attempts are over (connected or failed),
// so its blocks can already use OBS, Twitch, ...; it doesn't wait more than MaxWait for a slow one (e.g. a Twitch
// login waiting on the user)
public sealed class AppStartedManager : IDisposable
{
    // constants



    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(15);



    // private



    private readonly ChatbotComponentModel _model;
    private readonly Func<bool> _isConnecting;
    private readonly DateTime _startedAt;
    private readonly DispatcherTimer _check;



    // constructor



    // isConnecting: whether any connection is still making its startup attempt
    public AppStartedManager(ChatbotComponentModel model, Func<bool> isConnecting)
    {
        _model = model;
        _isConnecting = isConnecting;
        _startedAt = DateTime.UtcNow;
        _check = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        _check.Tick += OnCheck;
        _check.Start();
    }



    // callbacks



    private void OnCheck(object? sender, EventArgs e)
    {
        if (_isConnecting() && DateTime.UtcNow - _startedAt < MaxWait) return;

        Stop();
        _model.TryRunEvent(ChatEvents.AppStarted, new Dictionary<string, string> { ["timestamp"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss") });
    }



    // helpers



    private void Stop()
    {
        _check.Stop();
        _check.Tick -= OnCheck;
    }



    // disposal



    public void Dispose() => Stop();
}
