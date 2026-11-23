using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// a Clip box's settings (the longest clip it picks, empty: any; how many of the most viewed it picks from) and its
// preview: blank, or while a clip plays its still (Twitch's player only runs on the widget's page, so the still stands
// in for it here). A typed value applies when its box is left, a refused one is put back
public sealed partial class ClipBodyViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly ClipWidget _clip;
    private string _shownThumbnail; // the still asked for last, so a slow one for an earlier clip isn't shown

    [ObservableProperty] private string _maxSecondsInput; // empty: any length
    [ObservableProperty] private string _topInput;
    [ObservableProperty] private Bitmap? _preview;



    // constructor



    public ClipBodyViewModel(ClipWidget clip)
    {
        _clip = clip;
        _shownThumbnail = "";
        _maxSecondsInput = "";
        _topInput = "";
        ShowSettings();
        ShowPreview();

        _clip.SettingsChanged += ShowSettings;
        _clip.PlaybackChanged += ShowPreview;
    }



    // ui events



    public void Commit()
    {
        var text = MaxSecondsInput.Trim();
        if (text == "") _clip.TrySetMaxClipSeconds(null);
        else if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !_clip.TrySetMaxClipSeconds(seconds))
            MaxSecondsInput = MaxText();

        if (!int.TryParse(TopInput.Trim(), out var top) || !_clip.TrySetTopClips(top)) TopInput = TopText();
    }



    // model events



    private void ShowSettings()
    {
        MaxSecondsInput = MaxText();
        TopInput = TopText();
    }



    // the playing clip's still (fetched once per clip), nothing while idle; a still that can't be had leaves it blank
    private void ShowPreview()
    {
        var thumbnail = _clip.Playing?.Clip.Thumbnail ?? "";
        if (thumbnail == _shownThumbnail) return;

        _shownThumbnail = thumbnail;
        Preview = null;
        if (thumbnail != "") _ = LoadAsync(thumbnail);
    }



    private async Task LoadAsync(string url)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            if (url != _shownThumbnail) return;
            using var stream = new MemoryStream(bytes);
            Preview = new Bitmap(stream);
        }
        catch (Exception)
        {
            // only a stand-in for the page: it stays blank
        }
    }



    private string TopText() => _clip.TopClips.ToString(CultureInfo.InvariantCulture);



    private string MaxText() => _clip.MaxClipSeconds?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";



    // disposal



    public override void Dispose()
    {
        _clip.SettingsChanged -= ShowSettings;
        _clip.PlaybackChanged -= ShowPreview;
    }
}
