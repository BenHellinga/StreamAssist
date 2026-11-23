using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Widgets.Background;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Widgets.Containers;



public class PlayClipBlock : ContainerBlock
{
    // info



    [JsonIgnore] public override string Name => "Play Clip";
    [JsonIgnore] public override string Description => "Plays a random clip from the Twitch channel named in Channel (one of its Top most viewed no longer than Max Length, as the widget is set) on the clip widget named in Widget, with sound (after the ones already waiting, if one is playing), and carries on straight away with {clipTitle}, {clipUrl} and {channel}. Once it has ended (or been skipped with Next), When Done runs with them too. A channel with no clips (that fit) runs No Clips instead, with {channel}. Turned off: nothing happens.";
    [JsonIgnore] public override string? Subgroup => "Clip";



    // private



    private readonly MenuSlot _widget;
    private readonly ValueSlot<string> _channel;
    private readonly BlockSlot _whenDone;
    private readonly BlockSlot _noClips;
    private readonly IReadOnlyList<BlockSlotBase> _slots;



    // public



    [JsonIgnore] public override IReadOnlyList<BlockSlotBase> Slots => _slots;



    // json



    [JsonInclude] [JsonPropertyName("widget")]
    private ValueSlotData JsonWidget
    {
        get => _widget.GetData();
        set => _widget.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("channel")]
    private ValueSlotData JsonChannel
    {
        get => _channel.GetData();
        set => _channel.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("whenDone")]
    private PendingBlockData? JsonWhenDone
    {
        get => _whenDone.GetData();
        set => _whenDone.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("noClips")]
    private PendingBlockData? JsonNoClips
    {
        get => _noClips.GetData();
        set => _noClips.LoadData(value);
    }



    // constructor



    public PlayClipBlock()
    {
        _widget = WidgetBlocks.WidgetSlot(WidgetKind.Clip);
        _channel = new("Channel");
        _channel.SetText("{target}");
        _whenDone = new("When Done", typeof(ExecutableBlock));
        _noClips = new("No Clips", typeof(ExecutableBlock));
        _slots = [_widget, _channel, _whenDone, _noClips];

        TrackSlots();
    }



    // execute



    protected override async Task ExecuteAsync(TriggerContext context)
    {
        if (!WidgetBlocks.TryFind(await _widget.EvaluateAsync(context), WidgetKind.Clip, out var found, out var error)) throw Fail(error);
        var widget = (ClipWidget)found;
        if (widget.IsDisabled) return;

        var channel = (await _channel.EvaluateAsync(context)).Trim().TrimStart('@');
        if (channel == "") throw Fail("no channel");
        var chatbot = GlobalContext.ChatbotManager ?? throw Fail("chatbot not set up");

        var (ok, clip, name) = await ClipPicker.TryPickAsync(chatbot, widget, channel, context.CancellationToken);
        if (!ok) throw new BlockFailedException("clips not read");

        context.SetParameter("channel", name);
        if (clip == null)
        {
            await _noClips.RunAsync(context);
            return;
        }

        context.SetParameter("clipTitle", clip.Title);
        context.SetParameter("clipUrl", clip.Url);

        // its own copy of the parameters as they are now, and the binding's cancellation (a removed binding never runs
        // its end)
        var end = new TriggerContext(TriggerPhase.Once, context.CancellationToken, context.Parameters);
        widget.TryPlay(clip, () => RunWhenDone(end));
    }



    private void RunWhenDone(TriggerContext context)
    {
        if (context.CancellationToken.IsCancellationRequested || _whenDone.Block is not ExecutableBlock block) return;
        _ = block.SafeRunAsync(context);
    }
}
