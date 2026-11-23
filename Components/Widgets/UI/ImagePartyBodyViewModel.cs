using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;
using StreamAssist.Core.Persistence;

namespace StreamAssist.Components.Widgets.UI;



// which widget binding is open in the Widgets tab's bottom editor (at most one; for now an image party's When Corner Hit)
public static class WidgetBindingSelection
{
    private static ImagePartyWidget? _selected;

    public static ImagePartyWidget? Selected => _selected;

    public static event Action? Changed;



    // clicking the open bind box again closes it
    public static void Toggle(ImagePartyWidget party) => Set(_selected == party ? null : party);



    public static void Set(ImagePartyWidget? party)
    {
        if (_selected == party) return;

        _selected = party;
        Changed?.Invoke();
    }
}



// an Image Party box's settings (canvas size, randomness, size and speed multipliers for every item), its When Corner
// Hit bind box, its items (a row each: file, Browse…, size, speed, muted for a video; the empty last row adds one, emptying a
// file removes it) and its preview: the canvas with everything bouncing (an image as it is, a gif's first frame, a
// video as a box with its name). Typed values apply when their box is left, a refused one is put back
public sealed partial class ImagePartyBodyViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly ImagePartyWidget _party;
    private readonly Dictionary<string, Bitmap?> _bitmaps; // the preview's images by path, loaded once

    [ObservableProperty] private string _canvasWidthInput;
    [ObservableProperty] private string _canvasHeightInput;
    [ObservableProperty] private string _speedMultiplierInput;
    [ObservableProperty] private string _randomnessInput;
    [ObservableProperty] private string _sizeMultiplierInput;
    [ObservableProperty] private PartyPreviewFrame _previewFrame;



    // public



    public ObservableCollection<PartyItemRowViewModel> Rows { get; }
    public CornerBindBoxViewModel CornerBindBox { get; }



    // constructor



    public ImagePartyBodyViewModel(ImagePartyWidget party)
    {
        _party = party;
        _bitmaps = [];
        Rows = [];
        CornerBindBox = new(party);

        _canvasWidthInput = "";
        _canvasHeightInput = "";
        _speedMultiplierInput = "";
        _randomnessInput = "";
        _sizeMultiplierInput = "";
        _previewFrame = Frame();
        ShowSettings();
        SyncRows();

        _party.SettingsChanged += OnPartySettingsChanged;
        _party.ItemsChanged += SyncRows;
        _party.LiveChanged += OnPartyLiveChanged;
    }



    // ui events



    public void Commit()
    {
        if (!int.TryParse(CanvasWidthInput.Trim(), out var width) || !int.TryParse(CanvasHeightInput.Trim(), out var height) || !_party.TrySetCanvas(width, height))
        {
            CanvasWidthInput = _party.CanvasWidth.ToString(CultureInfo.InvariantCulture);
            CanvasHeightInput = _party.CanvasHeight.ToString(CultureInfo.InvariantCulture);
        }
        if (!TryParse(SpeedMultiplierInput, out var speed) || !_party.TrySetSpeedMultiplier(speed)) SpeedMultiplierInput = Format(_party.SpeedMultiplier);
        if (!TryParse(RandomnessInput, out var randomness) || !_party.TrySetRandomness(randomness)) RandomnessInput = Format(_party.Randomness);
        if (!TryParse(SizeMultiplierInput, out var multiplier) || !_party.TrySetSizeMultiplier(multiplier)) SizeMultiplierInput = Format(_party.SizeMultiplier);
    }



    // model events



    private void OnPartySettingsChanged()
    {
        ShowSettings();
        PreviewFrame = Frame();
    }



    private void OnPartyLiveChanged() => PreviewFrame = Frame();



    // helpers



    private void ShowSettings()
    {
        CanvasWidthInput = _party.CanvasWidth.ToString(CultureInfo.InvariantCulture);
        CanvasHeightInput = _party.CanvasHeight.ToString(CultureInfo.InvariantCulture);
        SpeedMultiplierInput = Format(_party.SpeedMultiplier);
        RandomnessInput = Format(_party.Randomness);
        SizeMultiplierInput = Format(_party.SizeMultiplier);
    }



    private PartyPreviewFrame Frame()
    {
        var items = _party.Frame();
        foreach (var item in items.Where(i => !i.IsVideo && !_bitmaps.ContainsKey(i.Path))) _bitmaps[item.Path] = Load(item.Path);
        return new(_party.CanvasWidth, _party.CanvasHeight, items, _bitmaps);
    }



    // an image (a gif's first frame) for the preview, null if it can't be read
    private static Bitmap? Load(string path)
    {
        try
        {
            return AppPaths.TryResolveAsset(path, out var full, out _) && System.IO.File.Exists(full) ? new Bitmap(full) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }



    // the rows follow the items, keeping the view model of each one that's still there (so the box being typed in
    // stays the same box)
    private void SyncRows()
    {
        var items = _party.Items;
        foreach (var gone in Rows.Where(r => !items.Contains(r.Item)).ToList())
        {
            Rows.Remove(gone);
            gone.Dispose();
        }

        for (var i = 0; i < items.Count; i++)
        {
            var at = Rows.ToList().FindIndex(r => r.Item == items[i]);
            if (at == i) continue;
            if (at >= 0) Rows.Move(at, i);
            else Rows.Insert(i, new PartyItemRowViewModel(_party, items[i]));
        }
    }



    private static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);



    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);



    // disposal



    public override void Dispose()
    {
        _party.SettingsChanged -= OnPartySettingsChanged;
        _party.ItemsChanged -= SyncRows;
        _party.LiveChanged -= OnPartyLiveChanged;
        foreach (var row in Rows) row.Dispose();
        CornerBindBox.Dispose();
        foreach (var bitmap in _bitmaps.Values) bitmap?.Dispose();
    }
}



// what the preview draws: the canvas size, the items where they are, and the images it has for them
public sealed record PartyPreviewFrame(int CanvasWidth, int CanvasHeight, IReadOnlyList<PartyItemState> Items, IReadOnlyDictionary<string, Bitmap?> Images);



// one item's row: its file (typed, applied when the box is left; or picked with Browse…), its size, its speed and, for a
// video, muted. The empty last row adds one, emptying a file removes it
public sealed partial class PartyItemRowViewModel : ViewModelBase, ICommitOnLeave
{
    // private



    private readonly ImagePartyWidget _party;
    private readonly PartyItem _item;

    [ObservableProperty] private string _pathInput;
    [ObservableProperty] private string _sizeInput;
    [ObservableProperty] private string _speedInput;
    [ObservableProperty] private bool _muted;
    [ObservableProperty] private bool _hasPath;
    [ObservableProperty] private bool _isVideo;



    // public



    public PartyItem Item => _item;



    // constructor



    public PartyItemRowViewModel(ImagePartyWidget party, PartyItem item)
    {
        _party = party;
        _item = item;
        _pathInput = "";
        _sizeInput = "";
        _speedInput = "";
        Show();

        _item.Changed += Show;
    }



    // ui events



    public void Commit()
    {
        if (PathInput.Trim() != _item.Path) _party.SetItemPath(_item, PathInput);
        if (!_item.IsEmpty && (!double.TryParse(SizeInput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var size) || !_party.TrySetItemSize(_item, size)))
            SizeInput = Format(_item.Size);
        if (!_item.IsEmpty && (!double.TryParse(SpeedInput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) || !_party.TrySetItemSpeed(_item, speed)))
            SpeedInput = Format(_item.Speed);
    }



    partial void OnMutedChanged(bool value)
    {
        if (value != _item.Muted) _party.SetItemMuted(_item, value);
    }



    [RelayCommand]
    private async Task OnBrowsePressed()
    {
        var (path, error) = await AssetPicker.PickAsync("Select an image, gif or video (inside assets)");
        if (error != "") AppLog.WriteError(error);
        if (path != null) _party.SetItemPath(_item, path);
    }



    // model events



    private void Show()
    {
        PathInput = _item.Path;
        SizeInput = Format(_item.Size);
        SpeedInput = Format(_item.Speed);
        Muted = _item.Muted;
        HasPath = !_item.IsEmpty;
        IsVideo = _item.IsVideo;
    }



    private static string Format(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);



    // disposal



    public override void Dispose() => _item.Changed -= Show;
}



// the When Corner Hit bind box, like a dock button's: click to edit its binding below, drop a binding on it, edit
// buttons on hover, it flashes when it runs
public sealed partial class CornerBindBoxViewModel : ViewModelBase, IDroppable, IEditButtons
{
    // private



    private readonly ImagePartyWidget _party;
    private BindableModel? _bindable;
    private readonly Flash _flash;

    [ObservableProperty] private bool _isBound;
    [ObservableProperty] private bool _isDisabled;
    [ObservableProperty] private bool _isTriggered;



    // public



    public bool CanCollapse => false;
    public bool IsCollapsed => false;
    public bool CanCopy => true;
    public bool CanToggle => true;
    [RelayCommand] private void OnCopyButtonPressed() { if (_bindable != null) BlockClipboard.CopyBindable(_bindable); }



    // constructor



    public CornerBindBoxViewModel(ImagePartyWidget party)
    {
        _party = party;
        _flash = new(isOn => IsTriggered = isOn);
        SetBindable(_party.CornerBindable);

        _party.CornerBindableChanged += OnCornerBindableChanged;
    }



    // ui events



    public void OnBindBoxClicked() => WidgetBindingSelection.Toggle(_party);



    [RelayCommand] private void OnToggleButtonPressed() => _bindable?.SetIsDisabled(!_bindable.IsDisabled);
    [RelayCommand] private void OnRemoveButtonPressed() => _party.UnbindCorner();
    [RelayCommand] private void OnCollapseButtonPressed() { }



    // drag drop: a whole binding (from a key or another bind box) while it has none



    public bool CanAcceptDropPayload(object payload) => payload is BindableDragPayload dragged && dragged.Bindable != _bindable && _party.CornerBindable == null;



    public bool PassesOn(object payload) => false;



    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (payload is not BindableDragPayload dragged) return;

        var copy = BindableModel.DeepCopy(dragged.Bindable);
        if (!_party.TryBindCorner(copy))
        {
            copy.Dispose();
            return;
        }
        dragged.RemoveFromSource?.Invoke(); // only once the copy is safely bound
    }



    public object? GetDragPayload() => _bindable == null ? null : new BindableDragPayload(_bindable, _party.UnbindCorner);



    // model events



    private void OnCornerBindableChanged() => SetBindable(_party.CornerBindable);
    private void OnBindableChanged() => IsBound = _bindable?.HasBlock ?? false;
    private void OnBindableIsDisabledChanged(bool isDisabled) => IsDisabled = isDisabled;
    private void OnBindableTriggered() => _flash.Trigger();



    // helpers



    private void SetBindable(BindableModel? bindable)
    {
        if (_bindable != null)
        {
            _bindable.Changed -= OnBindableChanged;
            _bindable.IsDisabledChanged -= OnBindableIsDisabledChanged;
            _bindable.Triggered -= OnBindableTriggered;
        }

        _bindable = bindable;
        IsBound = _bindable?.HasBlock ?? false;
        IsDisabled = _bindable?.IsDisabled ?? false;

        if (_bindable != null)
        {
            _bindable.Changed += OnBindableChanged;
            _bindable.IsDisabledChanged += OnBindableIsDisabledChanged;
            _bindable.Triggered += OnBindableTriggered;
        }
    }



    // disposal



    public override void Dispose()
    {
        _party.CornerBindableChanged -= OnCornerBindableChanged;
        SetBindable(null);
        _flash.Dispose();
    }
}
