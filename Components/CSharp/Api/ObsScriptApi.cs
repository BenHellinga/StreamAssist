using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OBS.Background;
using StreamAssist.Components.OBS.Model;
using StreamAssist.Core.Connections;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using ObsTransform = StreamAssist.Scripting.ObsTransform;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IObs: the OBS manager does each request and logs its own failures (so those throw already logged)
public sealed class ObsScriptApi(TriggerContext context) : ScriptApiPart(context), IObs
{
    private static ObsManager Obs => Need(GlobalContext.ObsManager, "obs");



    public bool IsConnected => UiThread.Run<bool>(() => GlobalContext.ObsManager?.Status == ConnectionStatus.Connected);



    // scenes and sources



    public Task<string> GetSceneAsync() => UiThread.RunAsync<string>(async () =>
    {
        var (ok, scene) = await Obs.TryGetCurrentSceneAsync();
        return ok ? scene! : throw Logged("scene not read");
    });



    public Task SetSceneAsync(string scene) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetCurrentSceneAsync(scene)).Ok) throw Logged("scene not switched");
    });



    public Task<IReadOnlyList<string>> GetScenesAsync() => UiThread.RunAsync<IReadOnlyList<string>>(async () =>
    {
        var (ok, scenes) = await Obs.TryGetScenesAsync();
        return ok ? scenes! : throw Logged("scenes not read");
    });



    public Task<IReadOnlyList<string>> GetSourcesAsync(string? scene = null) => UiThread.RunAsync<IReadOnlyList<string>>(async () =>
    {
        var (ok, sources) = await Obs.TryGetSourcesAsync(scene ?? "");
        return ok ? sources! : throw Logged("sources not read");
    });



    public Task<bool> IsSourceVisibleAsync(string source, string? scene = null) => UiThread.RunAsync<bool>(async () =>
    {
        var (ok, visible) = await Obs.TryGetSourceVisibleAsync(scene ?? "", source);
        return ok ? visible : throw Logged("visibility not read");
    });



    public Task SetSourceVisibleAsync(string source, Switch change, string? scene = null) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetSourceVisibleAsync(scene ?? "", source, Toggle(change))).Ok) throw Logged("source not shown/hidden");
    });



    // where sources are



    public Task<double> GetTransformAsync(string source, ObsTransform setting, string? scene = null) => UiThread.RunAsync<double>(async () =>
    {
        var (ok, value) = await Obs.TryGetTransformAsync(scene ?? "", source, TransformOf(setting));
        return ok ? value : throw Logged("transform not read");
    });



    public Task SetTransformAsync(string source, ObsTransform setting, double value, string? scene = null) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetTransformAsync(scene ?? "", source, TransformOf(setting), value)).Ok) throw Logged("transform not set");
    });



    public Task ResizeAsync(string source, double width, double height, ObsAnchor keepFixed = ObsAnchor.TopLeft, string? scene = null) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TryResizeAsync(scene ?? "", source, (_, _) => (width, height), Spaced(keepFixed))).Ok) throw Logged("not resized");
    });



    public Task ScaleByAsync(string source, double percent, ObsAnchor keepFixed = ObsAnchor.TopLeft, string? scene = null) => UiThread.RunAsync(async () =>
    {
        var obs = Obs;
        var factor = 1 + percent / 100;
        if (factor <= 0) throw Error("can't shrink by 100% or more");
        if (!(await obs.TryResizeAsync(scene ?? "", source, (w, h) => (w * factor, h * factor), Spaced(keepFixed))).Ok) throw Logged("not resized");
    });



    // audio



    public Task<double> GetVolumeAsync(string source) => UiThread.RunAsync<double>(async () =>
    {
        var (ok, volume) = await Obs.TryGetVolumeAsync(source);
        return ok ? volume : throw Logged("volume not read");
    });



    public Task SetVolumeAsync(string source, double db) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetVolumeAsync(source, db)).Ok) throw Logged("volume not set");
    });



    public Task<bool> IsMutedAsync(string source) => UiThread.RunAsync<bool>(async () =>
    {
        var (ok, muted) = await Obs.TryGetMutedAsync(source);
        return ok ? muted : throw Logged("mute not read");
    });



    public Task SetMutedAsync(string source, Switch change) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetMutedAsync(source, Toggle(change))).Ok) throw Logged("mute not changed");
    });



    // filters, outputs and settings



    public Task SetFilterAsync(string source, string filter, Switch change) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetFilterEnabledAsync(source, filter, Toggle(change))).Ok) throw Logged("filter not changed");
    });



    public Task<ObsOutputStatus> GetOutputAsync(ObsOutput output) => UiThread.RunAsync<ObsOutputStatus>(async () =>
    {
        var (ok, status) = await Obs.TryGetOutputAsync(Spaced(output));
        return ok ? new(status.Active, status.Time) : throw Logged($"{Spaced(output).ToLowerInvariant()} not read");
    });



    public Task SetOutputAsync(ObsOutput output, Switch change) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetOutputAsync(Spaced(output), Toggle(change))).Ok) throw Logged("output not changed");
    });



    public Task SaveReplayAsync() => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySaveReplayAsync()).Ok) throw Logged("replay not saved");
    });



    public Task<object> GetSourceSettingAsync(string source, string setting) => UiThread.RunAsync<object>(async () =>
    {
        var obs = Obs;
        if (setting.Trim() == "") throw Error("needs a setting name");
        var (ok, json) = await obs.TryGetSourceSettingAsync(source, setting.Trim());
        if (!ok) throw Logged("setting not read");

        return json.ValueKind switch
        {
            JsonValueKind.String => json.GetString()!,
            JsonValueKind.Number => json.GetDouble(),
            JsonValueKind.True or JsonValueKind.False => json.GetBoolean(),
            _ => json.GetRawText(),
        };
    });



    public Task SetSourceSettingAsync(string source, string setting, object value) => UiThread.RunAsync(async () =>
    {
        var obs = Obs;
        if (setting.Trim() == "") throw Error("needs a setting name");
        if (!(await obs.TrySetSourceSettingAsync(source, setting.Trim(), value)).Ok) throw Logged("setting not set");
    });



    public Task SetTextAsync(string source, string text) => UiThread.RunAsync(async () =>
    {
        if (!(await Obs.TrySetSourceSettingAsync(source, "text", text)).Ok) throw Logged("text not set");
    });



    // helpers



    private static ObsToggle Toggle(Switch change) => change switch { Switch.On => ObsToggle.On, Switch.Off => ObsToggle.Off, _ => ObsToggle.Toggle };

    private static OBS.Model.ObsTransform TransformOf(Scripting.ObsTransform setting) =>
        ObsTransforms.Find(Spaced(setting)) ?? throw Error($"unknown setting '{setting}'");

    // an enum value as the blocks' menus spell it, e.g. ReplayBuffer -> "Replay Buffer", PositionX -> "Position X"
    public static string Spaced<T>(T value) where T : struct, Enum => Regex.Replace(value.ToString(), "(?<=[a-z])(?=[A-Z])", " ");

    // a block menu's option as the enum, e.g. "Bottom Right" -> BottomRight
    public static T Parse<T>(string option) where T : struct, Enum => Enum.TryParse<T>(option.Replace(" ", ""), ignoreCase: true, out var value) ? value : throw Error($"no option '{option}'");
}
