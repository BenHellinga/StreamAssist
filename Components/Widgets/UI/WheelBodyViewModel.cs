using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// a Wheel box's settings (spin and winner times, sounds and their volumes, the output device), its options (a row each,
// the empty last row adds one) and its preview: the wheel as it rests, turning while it spins, then the winner shown
// for its winner time. Typed values apply when their box is left, a refused one is put back
public sealed partial class WheelBodyViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly WheelWidget _wheel;
    private readonly DispatcherTimer _redraw; // only while it spins or shows a winner
    private DateTime _winnerUntil;

    [ObservableProperty] private string _spinSecondsInput;
    [ObservableProperty] private string _showSecondsInput;
    [ObservableProperty] private string _tickSoundInput;
    [ObservableProperty] private string _winSoundInput;
    [ObservableProperty] private string _tickVolumeInput;
    [ObservableProperty] private string _winVolumeInput;
    [ObservableProperty] private string? _selectedOutputDevice;
    [ObservableProperty] private IReadOnlyList<WheelSlice> _previewSlices;
    [ObservableProperty] private double _previewRotation;
    [ObservableProperty] private int _previewHighlight;



    // public



    public OptionListViewModel Options { get; }
    public IReadOnlyList<string> OutputDevices { get; }



    // constructor



    public WheelBodyViewModel(WheelWidget wheel)
    {
        _wheel = wheel;
        _redraw = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _winnerUntil = DateTime.MinValue;
        Options = new(_wheel.OptionList, "New option…");
        OutputDevices = [AudioHelper.DefaultDevice, .. AudioHelper.GetDeviceNames()];

        _spinSecondsInput = "";
        _showSecondsInput = "";
        _tickSoundInput = "";
        _winSoundInput = "";
        _tickVolumeInput = "";
        _winVolumeInput = "";
        _previewSlices = [];
        _previewHighlight = -1;
        ShowSettings();
        ShowPreview();

        _redraw.Tick += OnRedraw;
        _wheel.SettingsChanged += OnWheelSettingsChanged;
        _wheel.SpinStarted += OnWheelSpinChanged;
        _wheel.SpinFinished += OnWheelSpinFinished;
        if (_wheel.IsSpinning) _redraw.Start();
    }



    // ui events



    public void Commit()
    {
        if (!TryParse(SpinSecondsInput, out var spin) || !_wheel.TrySetSpinSeconds(spin)) SpinSecondsInput = Format(_wheel.SpinSeconds);
        if (!TryParse(ShowSecondsInput, out var show) || !_wheel.TrySetShowSeconds(show)) ShowSecondsInput = Format(_wheel.ShowSeconds);
        if (!TryParse(TickVolumeInput, out var tick) || !_wheel.TrySetTickVolume(tick)) TickVolumeInput = Format(_wheel.TickVolume);
        if (!TryParse(WinVolumeInput, out var win) || !_wheel.TrySetWinVolume(win)) WinVolumeInput = Format(_wheel.WinVolume);
        _wheel.SetTickSound(TickSoundInput);
        _wheel.SetWinSound(WinSoundInput);
        ShowSettings();
    }



    partial void OnSelectedOutputDeviceChanged(string? value)
    {
        if (value != null) _wheel.SetOutputDevice(value);
    }



    [RelayCommand] private Task OnBrowseTickPressed() => BrowseAsync("Tick Sound", _wheel.SetTickSound);
    [RelayCommand] private Task OnBrowseWinPressed() => BrowseAsync("Win Sound", _wheel.SetWinSound);



    // model events



    private void OnWheelSettingsChanged()
    {
        ShowSettings();
        ShowPreview();
    }



    private void OnWheelSpinChanged()
    {
        _winnerUntil = DateTime.MinValue;
        _redraw.Start();
        ShowPreview();
    }



    private void OnWheelSpinFinished()
    {
        _winnerUntil = DateTime.UtcNow.AddSeconds(_wheel.ShowSeconds);
        _redraw.Start();
        ShowPreview();
    }



    private void OnRedraw(object? sender, EventArgs e)
    {
        ShowPreview();
        if (!_wheel.IsSpinning && DateTime.UtcNow >= _winnerUntil) _redraw.Stop();
    }



    // helpers



    private void ShowSettings()
    {
        SpinSecondsInput = Format(_wheel.SpinSeconds);
        ShowSecondsInput = Format(_wheel.ShowSeconds);
        TickSoundInput = _wheel.TickSound;
        WinSoundInput = _wheel.WinSound;
        TickVolumeInput = Format(_wheel.TickVolume);
        WinVolumeInput = Format(_wheel.WinVolume);
        SelectedOutputDevice = OutputDevices.Contains(_wheel.OutputDevice) ? _wheel.OutputDevice : AudioHelper.DefaultDevice;
    }



    // the preview: mid-spin where the spin is now, then the winner while it shows, otherwise the options as they rest
    private void ShowPreview()
    {
        if (_wheel.Spin is { } spin)
        {
            PreviewSlices = spin.Slices;
            PreviewRotation = spin.RotationAt(DateTime.UtcNow - spin.StartedAt);
            PreviewHighlight = -1;
            return;
        }

        var slices = WheelWidget.Slices(_wheel.FilledOptions);
        PreviewSlices = slices;
        PreviewRotation = _wheel.Rotation;
        PreviewHighlight = DateTime.UtcNow < _winnerUntil ? slices.FindIndex(s => s.Text == _wheel.LastWinner) : -1;
    }



    private async Task BrowseAsync(string what, Action<string> set)
    {
        var (path, error) = await AssetPicker.PickAsync($"Select {what} (inside assets)");
        if (error != "") AppLog.WriteError(error);
        if (path != null) set(path);
    }



    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);



    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);



    // disposal



    public override void Dispose()
    {
        _redraw.Stop();
        _redraw.Tick -= OnRedraw;
        _wheel.SettingsChanged -= OnWheelSettingsChanged;
        _wheel.SpinStarted -= OnWheelSpinChanged;
        _wheel.SpinFinished -= OnWheelSpinFinished;
        Options.Dispose();
    }
}
