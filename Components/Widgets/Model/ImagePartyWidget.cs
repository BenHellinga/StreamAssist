using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;
using StreamAssist.Core.Utilities;

namespace StreamAssist.Components.Widgets.Model;



// one thing bouncing around an Image Party: its file (inside Assets/: an image, a gif or a video), its width on the
// canvas and its speed (before the party's Size / Speed Multipliers; its height follows its shape) and, for a video,
// whether its sound is muted. A row of the party's growing list: the empty last row is where a new one is added, emptying one's path removes
// it. Where it is and which way it's going only live while the app runs
public sealed class PartyItem : IDisposable
{
    private string _path = "";
    private double _size = ImagePartyWidget.DefaultSize;
    private double _speed = ImagePartyWidget.DefaultSpeed;
    private bool _muted;

    public string Path => _path;
    public double Size => _size;
    public double Speed => _speed; // canvas pixels a second
    public bool Muted => _muted;
    public bool IsEmpty => _path == "";
    public bool IsVideo => ImagePartyWidget.IsVideoPath(_path);

    // this session: an id for its page, its shape (height / width, 1 until known), where it is and which way it's going
    public int Id { get; internal set; }
    public double Aspect { get; internal set; } = 1;
    public bool IsPlaced { get; internal set; }
    public double X { get; internal set; }
    public double Y { get; internal set; }
    public double DirX { get; internal set; }
    public double DirY { get; internal set; }
    internal double LastBounceX { get; set; } = double.NegativeInfinity; // when it last hit a side wall, a corner is both close together
    internal double LastBounceY { get; set; } = double.NegativeInfinity;
    public int SideBounces { get; internal set; } // this session, off the left / right walls
    public int TopBottomBounces { get; internal set; } // off the top / bottom walls

    public event Action? Changed;



    internal void SetPath(string path)
    {
        if (path == _path) return;
        _path = path;
        Aspect = 1;
        Changed?.Invoke();
    }



    internal void SetSize(double size)
    {
        if (size == _size) return;
        _size = size;
        Changed?.Invoke();
    }



    internal void SetSpeed(double speed)
    {
        if (speed == _speed) return;
        _speed = speed;
        Changed?.Invoke();
    }



    internal void SetMuted(bool muted)
    {
        if (muted == _muted) return;
        _muted = muted;
        Changed?.Invoke();
    }



    public void Dispose() { }
}



// how a party's item is saved
public sealed class PartyItemData
{
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("size")] public double Size { get; set; } = ImagePartyWidget.DefaultSize;
    [JsonPropertyName("speed")] public double Speed { get; set; } = ImagePartyWidget.DefaultSpeed;
    [JsonPropertyName("muted")] public bool Muted { get; set; }
}



// an item as it's drawn: its id, file, where it is and how big (canvas pixels), a video or not, muted or not
public readonly record struct PartyItemState(int Id, string Path, double X, double Y, double Width, double Height, bool IsVideo, bool Muted);



// images, gifs and videos bouncing around a canvas like a DVD logo, each its own size and speed (both scaled by the
// party's multipliers), passing through each other. At Randomness 0 a bounce is a mirror (they can fall into loops);
// above it each bounce's angle is nudged at random, up to ±90° at 1, evenly enough that the sides get about as many
// bounces as the top and bottom. Hitting a corner (both walls close together) runs
// its When Corner Hit binding with {image}. Its settings, items and binding are saved; where they are only lives while
// the app runs. The WidgetManager moves them (50 times a second) and learns their shapes, its pages draw them
public sealed class ImagePartyWidget : WidgetModel, IJsonOnDeserialized
{
    // constants



    public const double DefaultSize = 200;
    public const double DefaultSpeed = 200;
    public const double MaxSize = 4000;
    public const int MaxCanvas = 7680;
    public const double MaxSpeed = 5000;
    private const double CornerSeconds = 0.05; // both walls hit within this is a corner
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".mov", ".m4v", ".ogv"];



    // private



    private readonly GrowingList<PartyItem> _items;
    private int _canvasWidth;
    private int _canvasHeight;
    private double _speedMultiplier;
    private double _randomness;
    private double _sizeMultiplier;
    private BindableModel? _cornerBindable;
    private readonly Random _random;
    private double _time;
    private int _nextId;



    // public



    [JsonIgnore] public override WidgetKind Kind => WidgetKind.ImageParty;
    [JsonIgnore] public IReadOnlyList<PartyItem> Items => _items.Items; // the empty last row included
    [JsonIgnore] public IReadOnlyList<PartyItem> FilledItems => _items.NonEmptyItems.ToList();
    [JsonIgnore] public int CanvasWidth => _canvasWidth;
    [JsonIgnore] public int CanvasHeight => _canvasHeight;
    [JsonIgnore] public double SpeedMultiplier => _speedMultiplier; // every item's speed times this
    [JsonIgnore] public double Randomness => _randomness; // 0 to 1
    [JsonIgnore] public double SizeMultiplier => _sizeMultiplier; // every item's size times this
    [JsonIgnore] public BindableModel? CornerBindable => _cornerBindable; // When Corner Hit



    // json



    [JsonInclude] [JsonPropertyName("items")]
    private List<PartyItemData> JsonItems
    {
        get => _items.NonEmptyItems.Select(i => new PartyItemData { Path = i.Path, Size = i.Size, Speed = i.Speed, Muted = i.Muted }).ToList();
        set => _items.Load(value.Select(data =>
        {
            var item = new PartyItem();
            item.SetSize(data.Size is > 0 and <= MaxSize ? data.Size : DefaultSize);
            item.SetSpeed(data.Speed is >= 0 and <= MaxSpeed ? data.Speed : DefaultSpeed);
            item.SetMuted(data.Muted);
            item.SetPath(data.Path.Trim());
            return item;
        }));
    }

    [JsonInclude] [JsonPropertyName("canvasWidth")]
    private int JsonCanvasWidth
    {
        get => _canvasWidth;
        set => _canvasWidth = value is >= 100 and <= MaxCanvas ? value : 1920;
    }

    [JsonInclude] [JsonPropertyName("canvasHeight")]
    private int JsonCanvasHeight
    {
        get => _canvasHeight;
        set => _canvasHeight = value is >= 100 and <= MaxCanvas ? value : 1080;
    }

    [JsonInclude] [JsonPropertyName("speedMultiplier")]
    private double JsonSpeedMultiplier
    {
        get => _speedMultiplier;
        set => _speedMultiplier = value is >= 0 and <= 20 ? value : 1;
    }

    [JsonInclude] [JsonPropertyName("randomness")]
    private double JsonRandomness
    {
        get => _randomness;
        set => _randomness = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("sizeMultiplier")]
    private double JsonSizeMultiplier
    {
        get => _sizeMultiplier;
        set => _sizeMultiplier = value is >= 0.05 and <= 20 ? value : 1;
    }

    [JsonInclude] [JsonPropertyName("whenCornerHit")]
    private BindableModel? JsonCornerBindable
    {
        get => _cornerBindable;
        set => _cornerBindable = value;
    }



    // events



    public event Action? ItemsChanged; // one was added, removed or changed (saved)
    public event Action? LiveChanged; // they moved (this session, many times a second)
    public event Action? CornerBindableChanged; // bound, unbound, or a different binding



    // constructor



    public ImagePartyWidget()
    {
        _items = new(() => new PartyItem(), i => i.IsEmpty, SubscribeToItem, UnsubscribeFromItem);
        _items.Changed += OnItemsChanged;
        _canvasWidth = 1920;
        _canvasHeight = 1080;
        _speedMultiplier = 1;
        _randomness = 0;
        _sizeMultiplier = 1;
        _cornerBindable = null;
        _random = new Random();
        _time = 0;
        _nextId = 0;
        GiveIds();
    }



    // public api: items (also while it's off)



    // another one bouncing (the empty row filled in); the same file again is another copy
    public bool TryAddItem(string path, double? size = null, double? speed = null, bool muted = false)
    {
        var trimmed = path.Trim();
        if (trimmed == "")
        {
            AppLog.WriteError($"can't add to image party '{Name}', no file");
            return false;
        }
        if (size is { } s && !(s is > 0 and <= MaxSize))
        {
            AppLog.WriteError($"can't add '{trimmed}' to image party '{Name}', size {s} isn't above 0 and up to {MaxSize}");
            return false;
        }
        if (speed is { } v && !(v is >= 0 and <= MaxSpeed))
        {
            AppLog.WriteError($"can't add '{trimmed}' to image party '{Name}', speed {v} isn't 0 to {MaxSpeed}");
            return false;
        }

        var row = _items.Items[^1];
        row.SetSize(size ?? DefaultSize);
        row.SetSpeed(speed ?? DefaultSpeed);
        row.SetMuted(muted);
        row.SetPath(trimmed); // the row is filled now: the growing list adds the next empty one
        return true;
    }



    // every copy of that file (case ignored)
    public bool TryRemoveItems(string path)
    {
        var matches = FilledItems.Where(i => string.Equals(i.Path, path.Trim().Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)
            || string.Equals(i.Path, path.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            AppLog.WriteError($"can't remove '{path.Trim()}' from image party '{Name}', it isn't in it");
            return false;
        }

        foreach (var item in matches) item.SetPath(""); // empty rows go
        return true;
    }



    public void ClearItems()
    {
        foreach (var item in FilledItems) item.SetPath("");
    }



    // a row's file typed or picked (emptying it removes the row, filling the empty row adds one)
    public void SetItemPath(PartyItem item, string path) => item.SetPath(path.Trim());



    public bool TrySetItemSize(PartyItem item, double size)
    {
        if (!(size is > 0 and <= MaxSize))
        {
            AppLog.WriteError($"can't set an image party item's size to {size}, it's above 0 and up to {MaxSize}");
            return false;
        }

        item.SetSize(size);
        return true;
    }



    public bool TrySetItemSpeed(PartyItem item, double speed)
    {
        if (!(speed is >= 0 and <= MaxSpeed))
        {
            AppLog.WriteError($"can't set an image party item's speed to {speed}, it's 0 to {MaxSpeed}");
            return false;
        }

        item.SetSpeed(speed);
        return true;
    }



    public void SetItemMuted(PartyItem item, bool muted) => item.SetMuted(muted);



    // its shape (height / width), learned from its file or its page (a video's); a nonsense one is ignored
    public void SetItemAspect(int id, double aspect)
    {
        if (!(aspect is > 0.01 and < 100) || FilledItems.FirstOrDefault(i => i.Id == id) is not { } item || Math.Abs(item.Aspect - aspect) < 1e-6) return;

        item.Aspect = aspect;
        Keep(item);
        RaiseDrawChanged();
    }



    // public api: settings



    public bool TrySetCanvas(int width, int height)
    {
        if (width is < 100 or > MaxCanvas || height is < 100 or > MaxCanvas)
        {
            AppLog.WriteError($"can't set image party canvas to {width} x {height}, each side is 100 to {MaxCanvas}");
            return false;
        }

        if (width == _canvasWidth && height == _canvasHeight) return true;
        _canvasWidth = width;
        _canvasHeight = height;
        foreach (var item in FilledItems) Keep(item);
        RaiseSettingsChanged();
        return true;
    }



    public bool TrySetSpeedMultiplier(double multiplier) => TrySetNumber(multiplier, 0, 20, ref _speedMultiplier, "speed multiplier");
    public bool TrySetRandomness(double randomness) => TrySetNumber(randomness, 0, 1, ref _randomness, "randomness");
    public bool TrySetSizeMultiplier(double multiplier) => TrySetNumber(multiplier, 0.05, 20, ref _sizeMultiplier, "size multiplier");



    // public api: When Corner Hit



    public bool TryBindCorner(BindableModel bindable)
    {
        if (_cornerBindable != null)
        {
            AppLog.WriteError($"can't bind image party '{Name}''s When Corner Hit, already bound");
            return false;
        }

        _cornerBindable = bindable;
        bindable.Changed += OnCornerBindableChanged;
        bindable.AttachUnbindSelf(UnbindCorner);
        CornerBindableChanged?.Invoke();
        RaiseSettingsChanged();
        return true;
    }



    public void UnbindCorner()
    {
        if (_cornerBindable is not { } bindable) return;

        bindable.Changed -= OnCornerBindableChanged;
        bindable.AttachUnbindSelf(null);
        _cornerBindable = null;
        bindable.Dispose();
        CornerBindableChanged?.Invoke();
        RaiseSettingsChanged();
    }



    // public api: moving



    // moves them on by that much time (the WidgetManager, 50 times a second; the tests). Off, nothing moves. Gives the
    // items that hit a corner (their When Corner Hit has run)
    public List<PartyItem> Step(double seconds)
    {
        var corners = new List<PartyItem>();
        if (IsDisabled) return corners;

        _time += seconds;
        foreach (var item in FilledItems)
        {
            if (!item.IsPlaced) Place(item);
            var (w, h) = SizeOf(item);
            var speed = item.Speed * _speedMultiplier;
            item.X += item.DirX * speed * seconds;
            item.Y += item.DirY * speed * seconds;

            var hitX = Wall(item.X, w, _canvasWidth, out var x, out var awayX);
            var hitY = Wall(item.Y, h, _canvasHeight, out var y, out var awayY);
            item.X = x;
            item.Y = y;
            if (hitX) Bounce(item, horizontal: true, awayX);
            if (hitY) Bounce(item, horizontal: false, awayY);

            // both walls close together: a corner (once)
            if ((hitX || hitY) && Math.Abs(item.LastBounceX - item.LastBounceY) <= CornerSeconds)
            {
                item.LastBounceX = double.NegativeInfinity;
                item.LastBounceY = double.NegativeInfinity;
                corners.Add(item);
            }
        }

        LiveChanged?.Invoke();
        foreach (var item in corners)
            _cornerBindable?.TriggerOnce(new Dictionary<string, string> { ["image"] = item.Path });
        return corners;
    }



    // where everything is now (the page and the preview draw it)
    public List<PartyItemState> Frame() => FilledItems.Select(i =>
    {
        var (w, h) = SizeOf(i);
        return new PartyItemState(i.Id, i.Path, i.X, i.Y, w, h, i.IsVideo, i.Muted);
    }).ToList();



    // helpers



    public static bool IsVideoPath(string path) => VideoExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));



    public (double Width, double Height) SizeOf(PartyItem item)
    {
        var w = item.Size * _sizeMultiplier;
        return (w, w * item.Aspect);
    }



    // somewhere random, going a random way that isn't nearly straight along an edge (picked with the canvas squashed to
    // a square, like a bounce, so a wide canvas doesn't start it heading for the long walls)
    private void Place(PartyItem item)
    {
        var (w, h) = SizeOf(item);
        item.X = _random.NextDouble() * Math.Max(0, _canvasWidth - w);
        item.Y = _random.NextDouble() * Math.Max(0, _canvasHeight - h);
        var angle = (20 + _random.NextDouble() * 50) * Math.PI / 180 + _random.Next(4) * Math.PI / 2;
        var (dx, dy) = (Math.Cos(angle) * Math.Max(1, _canvasWidth - w), Math.Sin(angle) * Math.Max(1, _canvasHeight - h));
        var length = Math.Sqrt(dx * dx + dy * dy);
        item.DirX = dx / length;
        item.DirY = dy / length;
        item.IsPlaced = true;
    }



    // kept inside the canvas (after it or the item changed size)
    private void Keep(PartyItem item)
    {
        var (w, h) = SizeOf(item);
        item.X = Math.Clamp(item.X, 0, Math.Max(0, _canvasWidth - w));
        item.Y = Math.Clamp(item.Y, 0, Math.Max(0, _canvasHeight - h));
    }



    // past a wall along one axis: put back inside (mirrored), with which way is away from it (+1 off the low wall)
    private static bool Wall(double at, double size, double room, out double inside, out int away)
    {
        var max = Math.Max(0, room - size);
        away = 0;
        inside = at;
        if (at < 0)
        {
            inside = Math.Min(max, -at);
            away = 1;
        }
        else if (at > max)
        {
            inside = Math.Max(0, 2 * max - at);
            away = -1;
        }
        return away != 0;
    }



    // the mirror bounce, its angle off the wall nudged by up to ±90° × the randomness (folded back short of skimming the
    // wall); always heading away from it. Worked out with the canvas squashed to a square (the room the item has to move
    // in, each way): there all four walls are alike, so a wide canvas doesn't get more bounces off its long walls. The
    // nudge is around the mirror angle, not towards straight off the wall, so bounces don't drift into ping-ponging
    private void Bounce(PartyItem item, bool horizontal, int away)
    {
        var (w, h) = SizeOf(item);
        var (sx, sy) = (Math.Max(1, _canvasWidth - w), Math.Max(1, _canvasHeight - h));

        // the mirrored direction in the square, as an angle from straight off the wall
        var (ux, uy) = (item.DirX / sx, item.DirY / sy);
        var (normal, along) = horizontal ? (Math.Abs(ux), uy) : (Math.Abs(uy), ux);
        var angle = Math.Atan2(along, normal);

        const double limit = 85 * Math.PI / 180;
        angle += (_random.NextDouble() - 0.5) * Math.PI * _randomness;
        while (Math.Abs(angle) > limit) angle = Math.Sign(angle) * 2 * limit - angle;

        // back to the canvas
        (ux, uy) = horizontal ? (Math.Cos(angle) * away, Math.Sin(angle)) : (Math.Sin(angle), Math.Cos(angle) * away);
        var (dx, dy) = (ux * sx, uy * sy);
        var length = Math.Sqrt(dx * dx + dy * dy);

        item.DirX = dx / length;
        item.DirY = dy / length;
        if (horizontal)
        {
            item.LastBounceX = _time;
            item.SideBounces++;
        }
        else
        {
            item.LastBounceY = _time;
            item.TopBottomBounces++;
        }
    }



    private bool TrySetNumber(double value, double min, double max, ref double field, string what)
    {
        if (!(value >= min && value <= max))
        {
            AppLog.WriteError($"can't set image party {what} to {value}, it's {min} to {max}");
            return false;
        }

        if (value != field)
        {
            field = value;
            foreach (var item in FilledItems) Keep(item);
            RaiseSettingsChanged();
        }
        return true;
    }



    // each row its own id for its page (a new row gets the next)
    private void GiveIds()
    {
        foreach (var item in _items.Items.Where(i => i.Id == 0)) item.Id = ++_nextId;
    }



    // model callbacks



    private void OnItemsChanged()
    {
        GiveIds();
        ItemsChanged?.Invoke();
        RaiseSettingsChanged();
    }



    private void OnItemChanged()
    {
        _items.Normalize();
        GiveIds();
        foreach (var item in FilledItems) Keep(item);
        ItemsChanged?.Invoke();
        RaiseSettingsChanged();
    }



    private void OnCornerBindableChanged() => RaiseSettingsChanged();



    private void SubscribeToItem(PartyItem item) => item.Changed += OnItemChanged;
    private void UnsubscribeFromItem(PartyItem item) => item.Changed -= OnItemChanged;



    // loaded: its binding can remove itself, every row has an id
    void IJsonOnDeserialized.OnDeserialized()
    {
        GiveIds();
        if (_cornerBindable is { } bindable)
        {
            bindable.Changed += OnCornerBindableChanged;
            bindable.AttachUnbindSelf(UnbindCorner);
        }
    }



    // disposal



    public override void Dispose()
    {
        if (_cornerBindable is { } bindable)
        {
            bindable.Changed -= OnCornerBindableChanged;
            bindable.Dispose();
        }
        _items.Changed -= OnItemsChanged;
        _items.Dispose();
    }
}
