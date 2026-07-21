using StreamAssist.Components.Main.Model;
using StreamAssist.Components.AppAction.Model;
using System.Text.Json.Serialization;
using StreamAssist.Components.Core.Configurator;
using System.Collections.Generic;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.AppAction.Background;

namespace StreamAssist.AppActions;



public class MuteVoicemeeterInput : ActionModel
{
    // info



    [JsonIgnore] public override string Name => "Mute/Unmute Voicemeeter";
    [JsonIgnore] public override string Description => "Mute, unmute, or toggle the selected target in Voicemeeter (setup specific).";



    // private



    private string _setType;
    private string _inputTarget;
    private bool _playSound;



    // public



    [ConfigurableParameter("Set Type", ConfiguratorType.Menu)] [JsonPropertyName("setType")]
    public string SetType
    {
        get => _setType;
        set => SetParameter(ref _setType, value);
    }

    [ConfigurableParameter("Input Target", ConfiguratorType.Menu)] [JsonPropertyName("inputTarget")]
    public string InputTarget
    {
        get => _inputTarget;
        set => SetParameter(ref _inputTarget, value);
    }

    [ConfigurableParameter("Play Sound", ConfiguratorType.Boolean)] [JsonPropertyName("playSound")]
    public bool PlaySound
    {
        get => _playSound;
        set => SetParameter(ref _playSound, value);
    }




    // constructor



    public MuteVoicemeeterInput()
    {
        _setType = GetSetTypeMenuItems()[0];
        _inputTarget = GetInputTargetMenuItems()[0];
        _playSound = true;
    }



    // public



    public static List<string> GetSetTypeMenuItems() => ["Mute", "Unmute", "Toggle"];



    public static List<string> GetInputTargetMenuItems() => ["All", "Misc", "Obs"];



    // execute



    protected override void Execute()
    {
        var _voicemeeterManager = GlobalContext.VoicemeeterManager;

        if (_voicemeeterManager == null)
        {
            AppLog.WriteLine("Could not set mute of input - no Voicemeeter reference in global context");
            return;
        }

        var target = VoicemeeterManager.StereoInputTarget.All;
        switch (_inputTarget)
        {
            case "All": target = VoicemeeterManager.StereoInputTarget.All; break;
            case "Misc": target = VoicemeeterManager.StereoInputTarget.Misc; break;
            case "Obs": target = VoicemeeterManager.StereoInputTarget.Obs; break;
        }

        _voicemeeterManager.TryGetMute(target, out bool muted);
        var newState = false;

        switch (_setType)
        {
            case "Mute":
                _voicemeeterManager.TrySetMute(target, true);
                newState = true;
                break;

            case "Unmute":
                _voicemeeterManager.TrySetMute(target, false);
                newState = false;
                break;

            case "Toggle":
                _voicemeeterManager.TryToggleSetMute(target);
                newState = !muted;
                break;
        }

        if (_playSound && muted != newState)
        {
            string filepath;

            if (newState) filepath = "C:\\Users\\benhe\\Music\\mute.mp3";
            else filepath = "C:\\Users\\benhe\\Music\\unmute.mp3";

            AudioHelper.TryPlaySound(filepath);
        }
    }
}