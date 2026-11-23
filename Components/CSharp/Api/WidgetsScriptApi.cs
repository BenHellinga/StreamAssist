using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Background;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Persistence;
using StreamAssist.Scripting;
using StreamAssist.Shell;
using PlinkoResult = StreamAssist.Scripting.PlinkoResult;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.CSharp.Api;



// IWidgets: widgets by name in the widgets config in use (the model refuses unknown ones, with why). Blocks pass plain
// callbacks (they run their own slots); scripts' callbacks are awaited in the background, their failures logged
public sealed class WidgetsScriptApi(TriggerContext context) : ScriptApiPart(context), IWidgets
{
    public void SetVisible(string widget, Switch change) => UiThread.Run(() =>
    {
        var found = Find<WidgetModel>(widget, null);
        found.SetIsHidden(change switch { Switch.On => false, Switch.Off => true, _ => !found.IsHidden });
    });



    // wheels



    public void AddWheelOption(string wheel, string option, double weight = 1, string color = "") => UiThread.Run(() =>
    {
        if (!Find<WheelWidget>(wheel, WidgetKind.Wheel).TryAddOption(option, weight, color)) throw Logged("option not added");
    });



    public void RemoveWheelOption(string wheel, string option) => UiThread.Run(() =>
    {
        if (!Find<WheelWidget>(wheel, WidgetKind.Wheel).TryRemoveOption(option)) throw Logged("option not removed");
    });



    public void ClearWheel(string wheel) => UiThread.Run(() => Find<WheelWidget>(wheel, WidgetKind.Wheel).ClearOptions());

    public IReadOnlyList<string> GetWheelOptions(string wheel) => UiThread.Run<IReadOnlyList<string>>(() => Find<WheelWidget>(wheel, WidgetKind.Wheel).FilledOptions.Select(o => o.Text).ToList());

    public string GetWheelWinner(string wheel) => UiThread.Run<string>(() => Find<WheelWidget>(wheel, WidgetKind.Wheel).LastWinner);

    public bool IsWheelSpinning(string wheel) => UiThread.Run<bool>(() => Find<WheelWidget>(wheel, WidgetKind.Wheel).IsSpinning);



    // a script's callbacks run on a background thread as part of this run, their failures logged with what they were for
    public void SpinWheel(string wheel, Func<string, Task>? whenDone = null) =>
        SpinWheel(wheel, whenDone == null ? null : Later($"wheel '{wheel.Trim()}' when done", whenDone));



    // for blocks: onDone gets the winner (on a background thread)
    public void SpinWheel(string wheel, Action<string>? onDone)
    {
        onDone = UiThread.Later(onDone);
        UiThread.Run(() =>
        {
            // nothing to say (e.g. it's off): no error
            if (!Find<WheelWidget>(wheel, WidgetKind.Wheel).TryStartSpin(onDone, out var error) && error != "") throw Error(error);
        });
    }



    // plinko



    public void DropPlinko(string plinko, int balls, Func<string, int, Task>? whenEachLands = null, Func<IReadOnlyList<string>, string, Task>? whenDone = null) =>
        DropPlinko(plinko, balls,
            whenEachLands == null ? null : Later($"plinko '{plinko.Trim()}' when each lands", whenEachLands),
            whenDone == null ? null : Later($"plinko '{plinko.Trim()}' when done", whenDone));



    // for blocks: the callbacks run on a background thread
    public void DropPlinko(string plinko, int balls, Action<string, int>? onLand, Action<IReadOnlyList<string>, string>? onDone)
    {
        onLand = UiThread.Later(onLand);
        onDone = UiThread.Later(onDone);
        UiThread.Run(() =>
        {
            if (!Find<PlinkoWidget>(plinko, WidgetKind.Plinko).TryDrop(balls, onLand, onDone, out var error) && error != "") throw Error(error);
        });
    }



    public bool IsPlinkoDropping(string plinko) => UiThread.Run<bool>(() => Find<PlinkoWidget>(plinko, WidgetKind.Plinko).IsDropping);



    public PlinkoResult GetPlinkoResult(string plinko) => UiThread.Run<PlinkoResult>(() =>
    {
        var found = Find<PlinkoWidget>(plinko, WidgetKind.Plinko);
        return new(found.LastResults.ToList(), found.LastResult);
    });



    // image party



    public void AddPartyItem(string party, string file, double? size = null, double? speed = null, bool muted = false) => UiThread.Run(() =>
    {
        var found = Find<ImagePartyWidget>(party, WidgetKind.ImageParty);
        var path = file.Trim();
        if (path == "") throw Error("no file");
        if (!AppPaths.TryResolveAsset(path, out var full, out var pathError)) throw Error(pathError);
        if (!File.Exists(full)) throw Error($"no file '{path}' in assets");
        if (!found.TryAddItem(path, size, speed, muted)) throw Logged("item not added");
    });



    public void RemovePartyItems(string party, string file) => UiThread.Run(() =>
    {
        if (!Find<ImagePartyWidget>(party, WidgetKind.ImageParty).TryRemoveItems(file)) throw Logged("item not removed");
    });



    public void ClearParty(string party) => UiThread.Run(() => Find<ImagePartyWidget>(party, WidgetKind.ImageParty).ClearItems());



    // mic bounce



    public void SetBounceHeight(string bounce, double pixels) => UiThread.Run(() =>
    {
        if (!Find<MicBounceWidget>(bounce, WidgetKind.MicBounce).TrySetHeight(pixels)) throw Logged("height not set");
    });



    public void SetBounceImage(string bounce, string file, bool talking = false) => UiThread.Run(() =>
    {
        var found = Find<MicBounceWidget>(bounce, WidgetKind.MicBounce);
        if (talking) found.SetTalkingImage(file);
        else found.SetIdleImage(file);
    });



    // clips



    public async Task<TwitchClip?> PlayClipAsync(string clipWidget, string channel, Func<Task>? whenDone = null)
    {
        var (clip, _) = await PlayClipAsync(clipWidget, channel, whenDone == null ? null : Later($"clip '{clipWidget.Trim()}' when done", whenDone));
        return clip == null ? null : new(clip.Id, clip.Url, clip.Title, clip.Channel, clip.Seconds, clip.Thumbnail, 0);
    }



    // for blocks: the clip (null: none fit, or the widget is off) and the channel's display name; onDone runs on a
    // background thread
    public Task<(ClipInfo? Clip, string Channel)> PlayClipAsync(string clipWidget, string channel, Action? onDone)
    {
        onDone = UiThread.Later(onDone);
        return UiThread.RunAsync(() => PlayClipHereAsync(clipWidget, channel, onDone));
    }



    private async Task<(ClipInfo? Clip, string Channel)> PlayClipHereAsync(string clipWidget, string channel, Action? onDone)
    {
        var widget = Find<ClipWidget>(clipWidget, WidgetKind.Clip);
        if (widget.IsDisabled) return (null, "");

        var login = channel.Trim().TrimStart('@');
        if (login == "") throw Error("no channel");
        var chatbot = Need(GlobalContext.ChatbotManager, "chatbot");

        var (ok, clip, name) = await ClipPicker.TryPickAsync(chatbot, widget, login, Token);
        if (!ok) throw Logged("clips not read");
        if (clip != null) widget.TryPlay(clip, onDone);
        return (clip, name);
    }



    public bool IsClipPlaying(string clipWidget) => UiThread.Run<bool>(() => Find<ClipWidget>(clipWidget, WidgetKind.Clip).IsPlaying);



    // helpers



    private static T Find<T>(string name, WidgetKind? kind) where T : WidgetModel
    {
        var widgets = Need(GlobalContext.WidgetsComponentModel, "widgets");
        return widgets.TryFindWidget(name, kind, out var widget, out var error) ? (T)widget : throw Error(error);
    }
}
