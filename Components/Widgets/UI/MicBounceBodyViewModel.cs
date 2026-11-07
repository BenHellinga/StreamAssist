using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Widgets.UI;



// a microphone input to pick in the Microphone dropdown (null id: none)
public sealed record MicChoice(string Label, string? Id)
{
    public override string ToString() => Label;
}



// a Mic Bounce box's settings (its images, microphone, height, the dB range it rises over and its smoothing) and its preview: the image the page shows,
// risen as far as it is on stream right now. Typed values apply when their box is left, a refused one is put back
public sealed partial class MicBounceBodyViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly MicBounceWidget _widget;
    private readonly Dictionary<string, Bitmap?> _images; // loaded once per path

    [ObservableProperty] private string _idleImageInput;
    [ObservableProperty] private string _talkingImageInput;
    [ObservableProperty] private string _heightInput;
    [ObservableProperty] private string _minDbInput;
    [ObservableProperty] private string _maxDbInput;
    [ObservableProperty] private string _smoothingInput;
    [ObservableProperty] private MicChoice? _selectedMicrophone;
    [ObservableProperty] private Bitmap? _previewImage;
    [ObservableProperty] private double _previewLevel;
    [ObservableProperty] private double _previewHeight;



    // public



    public IReadOnlyList<MicChoice> Microphones { get; }



    // constructor



    public MicBounceBodyViewModel(MicBounceWidget widget, InputListModel? inputs)
    {
        _widget = widget;
        _images = [];

        // Windows' microphone inputs (the list a Microphone box picks from); one that's picked but not plugged in stays
        // in the list so it isn't lost
        var microphones = (inputs?.Devices ?? []).Where(d => d.Kind == DeviceKind.Microphone).ToList();
        Microphones =
        [
            new("None", null),
            .. microphones.Select(d => new MicChoice(d.GetDisplayLabel(), d.DevicePath)),
            .. widget.Microphone is { } picked && microphones.All(d => !string.Equals(d.DevicePath, picked, StringComparison.OrdinalIgnoreCase))
                ? [new MicChoice("Not connected", picked)] : Array.Empty<MicChoice>(),
        ];

        _idleImageInput = "";
        _talkingImageInput = "";
        _heightInput = "";
        _minDbInput = "";
        _maxDbInput = "";
        _smoothingInput = "";
        ShowSettings();
        ShowLive();

        _widget.SettingsChanged += OnWidgetSettingsChanged;
        _widget.LiveChanged += OnWidgetLiveChanged;
    }



    // ui events



    public void Commit()
    {
        _widget.SetIdleImage(IdleImageInput);
        _widget.SetTalkingImage(TalkingImageInput);
        if (!TryParse(HeightInput, out var height) || !_widget.TrySetHeight(height)) HeightInput = Format(_widget.Height);
        if (!TryParse(MinDbInput, out var min) || !TryParse(MaxDbInput, out var max) || !_widget.TrySetRange(min, max))
        {
            MinDbInput = Format(_widget.MinDb);
            MaxDbInput = Format(_widget.MaxDb);
        }
        if (!TryParse(SmoothingInput, out var smoothing) || !_widget.TrySetSmoothing(smoothing)) SmoothingInput = Format(_widget.Smoothing);
        ShowSettings(); // e.g. a typed path trimmed, or the widget is off
    }



    partial void OnSelectedMicrophoneChanged(MicChoice? value)
    {
        if (value != null) _widget.SetMicrophone(value.Id);
    }



    [RelayCommand] private Task OnBrowseIdlePressed() => BrowseAsync("Idle Image", _widget.SetIdleImage);
    [RelayCommand] private Task OnBrowseTalkingPressed() => BrowseAsync("Talking Image", _widget.SetTalkingImage);



    // model events



    private void OnWidgetSettingsChanged()
    {
        ShowSettings();
        ShowLive();
    }



    private void OnWidgetLiveChanged() => ShowLive();



    // helpers



    private void ShowSettings()
    {
        IdleImageInput = _widget.IdleImage;
        TalkingImageInput = _widget.TalkingImage;
        HeightInput = Format(_widget.Height);
        MinDbInput = Format(_widget.MinDb);
        MaxDbInput = Format(_widget.MaxDb);
        SmoothingInput = Format(_widget.Smoothing);
        SelectedMicrophone = Microphones.FirstOrDefault(m => string.Equals(m.Id, _widget.Microphone, StringComparison.OrdinalIgnoreCase)) ?? Microphones[0];
    }



    private void ShowLive()
    {
        PreviewImage = Load(_widget.ShownImage);
        PreviewLevel = _widget.Level;
        PreviewHeight = _widget.Height;
    }



    private async Task BrowseAsync(string what, Action<string> set)
    {
        var (path, error) = await AssetPicker.PickAsync($"Select {what} (inside assets)");
        if (error != "") AppLog.WriteError(error);
        if (path != null) set(path);
    }



    // the image at a path inside Assets/, null if there's none (or it can't be read)
    private Bitmap? Load(string path)
    {
        if (path == "") return null;
        if (_images.TryGetValue(path, out var cached)) return cached;

        Bitmap? image = null;
        try
        {
            if (AppPaths.TryResolveAsset(path, out var full, out _) && System.IO.File.Exists(full)) image = new Bitmap(full);
        }
        catch (Exception)
        {
            // not an image the preview can show (the page may still)
        }

        _images[path] = image;
        return image;
    }



    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);



    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);



    // disposal



    public override void Dispose()
    {
        _widget.SettingsChanged -= OnWidgetSettingsChanged;
        _widget.LiveChanged -= OnWidgetLiveChanged;
        foreach (var image in _images.Values) image?.Dispose();
    }
}
