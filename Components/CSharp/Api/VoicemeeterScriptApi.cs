using System;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IVoicemeeter: the voicemeeter manager logs its own failures (so those throw already logged)
public sealed class VoicemeeterScriptApi(TriggerContext context) : ScriptApiPart(context), IVoicemeeter
{
    private static VoicemeeterManager Voicemeeter => Need(GlobalContext.VoicemeeterManager, "voicemeeter");



    public bool IsConnected => UiThread.Run<bool>(() => GlobalContext.VoicemeeterManager?.Status == ConnectionStatus.Connected);



    public bool IsMuted(string channel) => UiThread.Run<bool>(() => Voicemeeter.TryGetMute(channel, out var muted) ? muted : throw Logged("mute not read"));



    public bool SetMuted(string channel, Switch change) => UiThread.Run<bool>(() =>
    {
        var voicemeeter = Voicemeeter;
        if (!voicemeeter.TryGetMute(channel, out var muted)) throw Logged("mute not read");
        var mute = change switch { Switch.On => true, Switch.Off => false, _ => !muted };
        if (!voicemeeter.TrySetMute(channel, mute)) throw Logged("mute not set");
        return mute;
    });



    public double GetGain(string channel) => UiThread.Run<double>(() => Voicemeeter.TryGetGain(channel, out var gain) ? gain : throw Logged("gain not read"));



    public void SetGain(string channel, double db) => UiThread.Run(() =>
    {
        if (!Voicemeeter.TrySetGain(channel, (float)db)) throw Logged("gain not set");
    });



    public bool IsRouted(string input, string bus) => UiThread.Run<bool>(() => Voicemeeter.TryGetRoute(input, bus, out var routed) ? routed : throw Logged("route not read"));



    public void SetRouted(string input, string bus, Switch change) => UiThread.Run(() =>
    {
        var voicemeeter = Voicemeeter;
        var routed = change == Switch.On;
        if (change == Switch.Toggle)
        {
            if (!voicemeeter.TryGetRoute(input, bus, out var current)) throw Logged("route not read");
            routed = !current;
        }
        if (!voicemeeter.TrySetRoute(input, bus, routed)) throw Logged("route not set");
    });



    public double GetParameter(string name) => UiThread.Run<double>(() =>
    {
        var voicemeeter = Voicemeeter;
        if (name.Trim() == "") throw Error("needs a parameter name");
        return voicemeeter.TryGetParameter(name.Trim(), out var value) ? value : throw Logged("parameter not read");
    });



    public void SetParameter(string name, double value) => UiThread.Run(() =>
    {
        var voicemeeter = Voicemeeter;
        if (name.Trim() == "") throw Error("needs a parameter name");
        if (!voicemeeter.TrySetParameter(name.Trim(), (float)value)) throw Logged("parameter not set");
    });
}
