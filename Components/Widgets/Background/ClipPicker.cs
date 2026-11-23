using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StreamAssist.Components.Chatbot.Background;
using StreamAssist.Components.Widgets.Model;

namespace StreamAssist.Components.Widgets.Background;



// picks a random clip of a channel for a clip widget (the Play Clip block, and the widget box's Play Clip through the
// WidgetManager): of its clips no longer than the widget's Max Length, one of the Top most viewed
public static class ClipPicker
{
    // Ok false: Twitch couldn't be asked (the chatbot said why). Clip null: the channel has none that fit. Channel: its
    // display name (as typed if it has no clips)
    public static async Task<(bool Ok, ClipInfo? Clip, string Channel)> TryPickAsync(ChatbotManager chatbot, ClipWidget widget, string channel, CancellationToken ct)
    {
        var (ok, clips) = await chatbot.TryGetClipsAsync(channel, ct);
        if (!ok || clips == null) return (false, null, channel);

        var fitting = clips.OrderByDescending(c => c.Views).Select(c => new ClipInfo(c.Id, c.Url, c.Title, c.Channel, c.Seconds, c.Thumbnail))
            .Where(widget.Allows).Take(widget.TopClips).ToList();
        var name = clips.FirstOrDefault()?.Channel ?? channel;
        return (true, fitting.Count == 0 ? null : fitting[Random.Shared.Next(fitting.Count)], name);
    }
}
