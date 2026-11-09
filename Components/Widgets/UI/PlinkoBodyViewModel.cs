using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Audio.Background;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// a Plinko box's settings (the board: rows, gravity, bounciness, whether balls collide; dropping: the tempo, max balls,
// how a drop's result is made (Most / Sum) and how long it shows; sounds, their volumes and the output device), its buckets (a row each, the empty last row adds
// one, drag a handle to reorder) and its preview: the board, with its balls as they fall. Typed values apply when their
// box is left, a refused one is put back
public sealed partial class PlinkoBodyViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly PlinkoWidget _plinko;

    [ObservableProperty] private string _rowsInput;
    [ObservableProperty] private string _gravityInput;
    [ObservableProperty] private string _bouncinessInput;
    [ObservableProperty] private bool _ballsCollide;
    [ObservableProperty] private string _dropTempoInput;
    [ObservableProperty] private string _maxBallsInput; // empty: no limit
    [ObservableProperty] private string _resultsSecondsInput;
    [ObservableProperty] private string _pinSoundInput;
    [ObservableProperty] private string _winSoundInput;
    [ObservableProperty] private string _pinVolumeInput;
    [ObservableProperty] private string _winVolumeInput;
    [ObservableProperty] private string _selectedMode;
    [ObservableProperty] private string? _selectedOutputDevice;
    [ObservableProperty] private PlinkoFrame _previewFrame;



    // public



    public OptionListViewModel Buckets { get; }
    public IReadOnlyList<string> OutputDevices { get; }
    public IReadOnlyList<string> Modes { get; } = Enum.GetNames<PlinkoMode>();



    // constructor



    public PlinkoBodyViewModel(PlinkoWidget plinko)
    {
        _plinko = plinko;
        Buckets = new(_plinko.BucketList, "New bucket…");
        OutputDevices = [AudioHelper.DefaultDevice, .. AudioHelper.GetDeviceNames()];

        _rowsInput = "";
        _gravityInput = "";
        _bouncinessInput = "";
        _dropTempoInput = "";
        _maxBallsInput = "";
        _resultsSecondsInput = "";
        _pinSoundInput = "";
        _winSoundInput = "";
        _pinVolumeInput = "";
        _winVolumeInput = "";
        _selectedMode = _plinko.Mode.ToString();
        _previewFrame = _plinko.Frame();
        ShowSettings();

        _plinko.SettingsChanged += OnPlinkoSettingsChanged;
        _plinko.LiveChanged += OnPlinkoLiveChanged;
    }



    // ui events



    public void Commit()
    {
        if (!int.TryParse(RowsInput.Trim(), out var rows) || !_plinko.TrySetRows(rows)) RowsInput = _plinko.Rows.ToString(CultureInfo.InvariantCulture);
        if (!TryParse(GravityInput, out var gravity) || !_plinko.TrySetGravity(gravity)) GravityInput = Format(_plinko.Gravity);
        if (!TryParse(BouncinessInput, out var bounce) || !_plinko.TrySetBounciness(bounce)) BouncinessInput = Format(_plinko.Bounciness);
        if (!TryParse(DropTempoInput, out var tempo) || !_plinko.TrySetDropTempo(tempo)) DropTempoInput = Format(_plinko.DropTempo);
        if (!TryParse(ResultsSecondsInput, out var results) || !_plinko.TrySetResultsSeconds(results)) ResultsSecondsInput = Format(_plinko.ResultsSeconds);
        if (!TryParse(PinVolumeInput, out var pin) || !_plinko.TrySetPinVolume(pin)) PinVolumeInput = Format(_plinko.PinVolume);
        if (!TryParse(WinVolumeInput, out var win) || !_plinko.TrySetWinVolume(win)) WinVolumeInput = Format(_plinko.WinVolume);

        var max = MaxBallsInput.Trim();
        if (max == "") _plinko.TrySetMaxBalls(null);
        else if (!int.TryParse(max, out var count) || !_plinko.TrySetMaxBalls(count)) MaxBallsInput = MaxText();

        _plinko.SetPinSound(PinSoundInput);
        _plinko.SetWinSound(WinSoundInput);
        ShowSettings();
    }



    partial void OnBallsCollideChanged(bool value) => _plinko.SetBallsCollide(value);



    partial void OnSelectedModeChanged(string value)
    {
        if (Enum.TryParse<PlinkoMode>(value, out var mode)) _plinko.SetMode(mode);
    }



    partial void OnSelectedOutputDeviceChanged(string? value)
    {
        if (value != null) _plinko.SetOutputDevice(value);
    }



    [RelayCommand] private Task OnBrowsePinPressed() => BrowseAsync("Pin Sound", _plinko.SetPinSound);
    [RelayCommand] private Task OnBrowseWinPressed() => BrowseAsync("Win Sound", _plinko.SetWinSound);



    // model events



    private void OnPlinkoSettingsChanged()
    {
        ShowSettings();
        PreviewFrame = _plinko.Frame();
    }



    private void OnPlinkoLiveChanged() => PreviewFrame = _plinko.Frame();



    // helpers



    private void ShowSettings()
    {
        RowsInput = _plinko.Rows.ToString(CultureInfo.InvariantCulture);
        GravityInput = Format(_plinko.Gravity);
        BouncinessInput = Format(_plinko.Bounciness);
        BallsCollide = _plinko.BallsCollide;
        DropTempoInput = Format(_plinko.DropTempo);
        MaxBallsInput = MaxText();
        ResultsSecondsInput = Format(_plinko.ResultsSeconds);
        PinSoundInput = _plinko.PinSound;
        WinSoundInput = _plinko.WinSound;
        PinVolumeInput = Format(_plinko.PinVolume);
        WinVolumeInput = Format(_plinko.WinVolume);
        SelectedMode = _plinko.Mode.ToString();
        SelectedOutputDevice = OutputDevices.Contains(_plinko.OutputDevice) ? _plinko.OutputDevice : AudioHelper.DefaultDevice;
    }



    private string MaxText() => _plinko.MaxBalls?.ToString(CultureInfo.InvariantCulture) ?? "";



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
        _plinko.SettingsChanged -= OnPlinkoSettingsChanged;
        _plinko.LiveChanged -= OnPlinkoLiveChanged;
        Buckets.Dispose();
    }
}
