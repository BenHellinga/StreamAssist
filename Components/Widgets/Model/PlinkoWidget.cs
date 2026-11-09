using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Widgets.Model;



// a straight line of the board balls bounce off (a wall, the floor, a bucket's divider), in board units
public readonly record struct PlinkoSegment(double X1, double Y1, double X2, double Y2);



// a bucket at the bottom, from Left to Right (board units), as wide as its share of the weights
public sealed record PlinkoBucket(string Text, string Color, double Left, double Right);



// the board's shape, worked out from its rows and buckets (the page and the preview draw it, the physics uses it). One
// board unit is the gap between two pins in a row; y goes down, the top row of pins at 0. Row r has r + 3 pins, so a
// triangle; the walls follow its sides through one more pin at each end of every row (on the wall), so to a ball the edge
// is just another pin with the usual gap beside it, and only going past those does it meet the wall. A lid
// closes the top over where balls drop in (not drawn), then the walls go straight down past the last row to the floor,
// the bottom split into the buckets by dividers, each one's name over its count in its middle (in front of the balls, as
// big as fits)
public sealed record PlinkoBoard(
    int Rows, double PinRadius, double BallRadius, IReadOnlyList<PlinkoSegment> Segments, IReadOnlyList<PlinkoSegment> Drawn,
    IReadOnlyList<PlinkoBucket> Buckets, double BucketTop, double Floor, double Left, double Right, double Top, double Bottom)
{
    public const double PinR = 0.1;
    public const double BallR = 0.25;
    public const double WallOffset = 1; // where one more pin would be, outside the outer pins: the wall's pins are there
    public const double LidY = -1.5;
    public const double SpawnY = -0.9; // the middle of where balls drop in: a spot within SpawnHalfWidth / Height of it
    public const double SpawnHalfWidth = 0.8;
    public const double SpawnHalfHeight = 0.3;
    public const double SegmentHalfWidth = 0.03;
    public const double LabelMax = 0.8; // a bucket's name and count, as big as the name fits its width between these (then cut with …)
    public const double LabelMin = 0.25;

    // the pins, row by row: row r has r + 3, a unit apart, centred, and one more at each end on the wall
    public IEnumerable<(double X, double Y)> Pins()
    {
        for (var row = 0; row < Rows; row++)
        {
            var count = row + 3;
            for (var i = -1; i <= count; i++) yield return (i - (count - 1) / 2.0, row);
        }
    }

    // where a drop's result shows: in the middle of the pins
    public double ResultY => (Rows - 1) / 2.0;

    // where the side walls are at height y (each one's distance from the middle)
    public static double WallX(double y) => 1 + y / 2 + WallOffset;

    // the bucket a ball at x is over, -1 with no buckets
    public int BucketAt(double x)
    {
        if (Buckets.Count == 0) return -1;
        for (var i = 0; i < Buckets.Count - 1; i++)
            if (x < Buckets[i].Right) return i;
        return Buckets.Count - 1;
    }

    public static PlinkoBoard Build(int rows, IReadOnlyList<WidgetOption> buckets)
    {
        var bottom = rows - 1;
        var bottomX = WallX(bottom);
        var bucketTop = rows - 0.3;
        var floor = bucketTop + 2.5;
        var lidX = WallX(LidY);

        var walls = new List<PlinkoSegment>
        {
            new(-lidX, LidY, -bottomX, bottom), new(lidX, LidY, bottomX, bottom), // the slanted sides
            new(-bottomX, bottom, -bottomX, floor), new(bottomX, bottom, bottomX, floor), // straight down past the pins
            new(-bottomX, floor, bottomX, floor), // the floor
        };

        // the buckets across the bottom by weight, a divider between each two
        var list = new List<PlinkoBucket>();
        var total = buckets.Sum(b => b.Weight);
        var left = -bottomX;
        foreach (var bucket in buckets)
        {
            var right = left + bucket.Weight / total * 2 * bottomX;
            list.Add(new(bucket.Text, bucket.Color, left, right));
            left = right;
        }
        for (var i = 0; i < list.Count - 1; i++) walls.Add(new(list[i].Right, bucketTop, list[i].Right, floor));

        var lid = new PlinkoSegment(-lidX, LidY, lidX, LidY);
        return new(rows, PinR, BallR, [.. walls, lid], walls, list, bucketTop, floor, -bottomX - 0.3, bottomX + 0.3, LidY - 0.3, floor + 0.3);
    }
}



// a ball as it's drawn
public readonly record struct PlinkoBallState(int Id, double X, double Y, string Color);



// a finished drop's result in front of the board: its text, the colour of its box's border, how faded in it is
public sealed record PlinkoResult(string Text, string Color, double Alpha);



// how a drop's buckets make its result: the bucket the most balls landed in (the first to get there, on a tie), or the
// buckets (numbers) added up
[JsonConverter(typeof(JsonStringEnumConverter<PlinkoMode>))]
public enum PlinkoMode { Most, Sum }



// everything the board shows right now: its shape, the balls, how faded they are (going once they've shown their
// results), how many have landed in each bucket, and the last drop's result while it shows
public sealed record PlinkoFrame(PlinkoBoard Board, IReadOnlyList<PlinkoBallState> Balls, double Alpha, IReadOnlyList<int> Counts, PlinkoResult? Result);



// a plinko board: a triangle of pins balls are dropped into (its header's Drop, or the Drop Plinko block), falling with
// gravity, bouncing off the pins, the walls and (if they collide) each other, into the buckets at the bottom, each as
// wide as its weight, so the board's shape decides the odds. Balls drop in one at a time, at most one every Drop Tempo,
// each at a random spot in a small area above the pins (picked once, it waits there until no ball is in the way). Once a
// drop's balls have all landed, its result (Most or Sum) shows in front with the win sound for the Results Time, then
// fades, and then its When Done runs; the balls fade with it once nothing's falling. Its settings are saved; the balls
// only live while the app runs. The WidgetManager steps it (50 times a second) and plays its sounds, its pages draw it
public sealed class PlinkoWidget : WidgetModel, IJsonOnDeserialized
{
    // constants



    public const int MaxRows = 40;
    public const double MaxSeconds = 60;
    private const double StepSeconds = 1.0 / 240; // the physics' step: small enough that a fast ball can't skip a pin
    private const double BaseGravity = 25; // board units / s², at Gravity 1
    private const double MaxSpeed = 25;
    private const double StuckSeconds = 30; // a ball still falling after this lands where it is
    private const double FadeInSeconds = 0.3; // the result
    private const double FadeSeconds = 0.4; // the result and the balls, going



    // private: settings



    private readonly WidgetOptionList _buckets;
    private int _rows;
    private double _gravity;
    private double _bounciness;
    private bool _ballsCollide;
    private double _dropTempo;
    private int? _maxBalls; // per drop, null: no limit
    private double _resultsSeconds;
    private string _pinSound;
    private string _winSound;
    private double _pinVolume;
    private double _winVolume;
    private PlinkoMode _mode;
    private string _outputDevice;



    // private: this session



    private sealed class Drop(int count, Action<string, int>? onLand, Action<IReadOnlyList<string>, string>? onDone)
    {
        public int Count { get; } = count;
        public List<string> Results { get; } = [];
        public Action<string, int>? OnLand { get; } = onLand;
        public Action<IReadOnlyList<string>, string>? OnDone { get; } = onDone;
    }

    private sealed class Ball
    {
        public int Id;
        public double X, Y, VX, VY;
        public string Color = "";
        public int Bucket = -1; // landed in, -1 while it falls
        public double Born;
        public Drop Drop = null!;
        public int Number; // its place in its drop, from 1
    }

    private PlinkoBoard _board;
    private readonly List<Ball> _balls;
    private readonly Queue<(Drop Drop, int Number)> _queue; // waiting to drop in
    private (double X, double Y)? _spawnAt; // where the next ball drops in, picked once, waiting for it to be clear
    private double _time; // simulated seconds (only moves while there's something on the board)
    private double _lastSpawn;
    private double _carry; // time not stepped yet
    private double? _settledAt; // when the last ball landed
    private double _alpha;
    private int[] _counts;
    private IReadOnlyList<string> _lastResults;
    private string _lastResult;
    private (string Text, string Color, double At)? _result; // the last finished drop's result, and when it began showing
    private readonly List<(double Due, Action Run)> _pendingDone; // When Dones waiting for their result to go
    private readonly Random _random;
    private int _nextId;



    // public



    [JsonIgnore] public override WidgetKind Kind => WidgetKind.Plinko;
    [JsonIgnore] public WidgetOptionList BucketList => _buckets; // its box's rows
    [JsonIgnore] public IReadOnlyList<WidgetOption> Buckets => _buckets.Filled;
    [JsonIgnore] public int Rows => _rows;
    [JsonIgnore] public double Gravity => _gravity; // 1 = normal
    [JsonIgnore] public double Bounciness => _bounciness; // 0 to 1
    [JsonIgnore] public bool BallsCollide => _ballsCollide;
    [JsonIgnore] public double DropTempo => _dropTempo; // seconds between balls at least
    [JsonIgnore] public int? MaxBalls => _maxBalls;
    [JsonIgnore] public double ResultsSeconds => _resultsSeconds;
    [JsonIgnore] public string PinSound => _pinSound; // inside Assets/, empty: the built-in tick
    [JsonIgnore] public string WinSound => _winSound; // empty: the built-in chime
    [JsonIgnore] public double PinVolume => _pinVolume;
    [JsonIgnore] public double WinVolume => _winVolume;
    [JsonIgnore] public PlinkoMode Mode => _mode;
    [JsonIgnore] public string OutputDevice => _outputDevice;
    [JsonIgnore] public PlinkoBoard Board => _board;
    [JsonIgnore] public bool IsDropping => _queue.Count > 0 || _balls.Any(b => b.Bucket < 0); // balls waiting or falling
    [JsonIgnore] public bool HasBalls => _balls.Count > 0 || _queue.Count > 0;
    [JsonIgnore] public bool IsBusy => HasBalls || _pendingDone.Count > 0 || _result != null; // something still to move, show or run
    [JsonIgnore] public IReadOnlyList<string> LastResults => _lastResults; // the last finished drop's buckets, in the order they landed
    [JsonIgnore] public string LastResult => _lastResult; // and its result (the most landed in, or the sum)



    // json



    [JsonInclude] [JsonPropertyName("buckets")]
    private List<WidgetOptionData> JsonBuckets
    {
        get => _buckets.GetData();
        set => _buckets.LoadData(value);
    }

    [JsonInclude] [JsonPropertyName("rows")]
    private int JsonRows
    {
        get => _rows;
        set => _rows = value is >= 1 and <= MaxRows ? value : 12;
    }

    [JsonInclude] [JsonPropertyName("gravity")]
    private double JsonGravity
    {
        get => _gravity;
        set => _gravity = value is >= 0.1 and <= 10 ? value : 1;
    }

    [JsonInclude] [JsonPropertyName("bounciness")]
    private double JsonBounciness
    {
        get => _bounciness;
        set => _bounciness = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("ballsCollide")]
    private bool JsonBallsCollide
    {
        get => _ballsCollide;
        set => _ballsCollide = value;
    }

    [JsonInclude] [JsonPropertyName("dropTempo")]
    private double JsonDropTempo
    {
        get => _dropTempo;
        set => _dropTempo = value is >= 0 and <= MaxSeconds ? value : 0.3;
    }

    [JsonInclude] [JsonPropertyName("maxBalls")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    private int? JsonMaxBalls
    {
        get => _maxBalls;
        set => _maxBalls = value is >= 1 ? value : null;
    }

    [JsonInclude] [JsonPropertyName("resultsSeconds")]
    private double JsonResultsSeconds
    {
        get => _resultsSeconds;
        set => _resultsSeconds = value is >= 0 and <= MaxSeconds ? value : 4;
    }

    [JsonInclude] [JsonPropertyName("pinSound")]
    private string JsonPinSound
    {
        get => _pinSound;
        set => _pinSound = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("winSound")]
    private string JsonWinSound
    {
        get => _winSound;
        set => _winSound = value ?? "";
    }

    [JsonInclude] [JsonPropertyName("pinVolume")]
    private double JsonPinVolume
    {
        get => _pinVolume;
        set => _pinVolume = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("winVolume")]
    private double JsonWinVolume
    {
        get => _winVolume;
        set => _winVolume = Math.Clamp(value, 0, 1);
    }

    [JsonInclude] [JsonPropertyName("mode")]
    private PlinkoMode JsonMode
    {
        get => _mode;
        set => _mode = value;
    }

    [JsonInclude] [JsonPropertyName("outputDevice")]
    private string JsonOutputDevice
    {
        get => _outputDevice;
        set => _outputDevice = value ?? "Default";
    }



    // events



    public event Action? BucketsChanged; // one was added, removed, moved or changed (saved)
    public event Action? DropStarted; // balls were queued
    public event Action? LiveChanged; // the balls moved (this session, many times a second)



    // constructor



    public PlinkoWidget()
    {
        _buckets = new("plinko bucket", "plinko board", () => Name);
        _buckets.Changed += OnBucketsChanged;
        _rows = 12;
        _gravity = 1;
        _bounciness = 0.5;
        _ballsCollide = true;
        _dropTempo = 0.3;
        _maxBalls = null;
        _resultsSeconds = 4;
        _pinSound = "";
        _winSound = "";
        _pinVolume = 0.3;
        _winVolume = 0.5;
        _mode = PlinkoMode.Most;
        _outputDevice = "Default";

        _balls = [];
        _queue = new();
        _spawnAt = null;
        _time = 0;
        _lastSpawn = double.NegativeInfinity;
        _carry = 0;
        _settledAt = null;
        _alpha = 1;
        _counts = [];
        _lastResults = [];
        _lastResult = "";
        _result = null;
        _pendingDone = [];
        _random = new Random();
        _nextId = 0;
        _board = PlinkoBoard.Build(_rows, []);
    }



    // public api: buckets (also while it's off; see WidgetOptionList)



    public bool TryAddBucket(string text, double weight = 1, string color = "") => _buckets.TryAdd(text, weight, color);
    public bool TryRemoveBucket(string text) => _buckets.TryRemove(text);
    public void ClearBuckets() => _buckets.Clear();



    // public api: settings



    public bool TrySetRows(int rows)
    {
        if (rows is < 1 or > MaxRows)
        {
            AppLog.WriteError($"can't set plinko rows to {rows}, it's 1 to {MaxRows}");
            return false;
        }

        if (rows != _rows)
        {
            _rows = rows;
            RebuildBoard();
            RaiseSettingsChanged();
        }
        return true;
    }



    public bool TrySetGravity(double gravity) => TrySetNumber(gravity, 0.1, 10, ref _gravity, "gravity");
    public bool TrySetBounciness(double bounciness) => TrySetNumber(bounciness, 0, 1, ref _bounciness, "bounciness");
    public bool TrySetDropTempo(double seconds) => TrySetNumber(seconds, 0, MaxSeconds, ref _dropTempo, "drop tempo");
    public bool TrySetResultsSeconds(double seconds) => TrySetNumber(seconds, 0, MaxSeconds, ref _resultsSeconds, "results time");
    public bool TrySetPinVolume(double volume) => TrySetNumber(volume, 0, 1, ref _pinVolume, "pin volume");
    public bool TrySetWinVolume(double volume) => TrySetNumber(volume, 0, 1, ref _winVolume, "win volume");



    public void SetMode(PlinkoMode mode)
    {
        if (mode == _mode) return;
        _mode = mode;
        RaiseSettingsChanged();
    }



    public void SetBallsCollide(bool collide)
    {
        if (collide == _ballsCollide) return;
        _ballsCollide = collide;
        RaiseSettingsChanged();
    }



    // null: no limit
    public bool TrySetMaxBalls(int? max)
    {
        if (max is < 1)
        {
            AppLog.WriteError($"can't set plinko max balls to {max}, it's 1 or more (empty: no limit)");
            return false;
        }

        if (max != _maxBalls)
        {
            _maxBalls = max;
            RaiseSettingsChanged();
        }
        return true;
    }



    public void SetPinSound(string path) => SetText(ref _pinSound, path.Trim());
    public void SetWinSound(string path) => SetText(ref _winSound, path.Trim());
    public void SetOutputDevice(string device) => SetText(ref _outputDevice, device.Trim() == "" ? "Default" : device.Trim());



    // public api: dropping



    // queues count balls (they drop in one at a time). False with an error for a bad count, one over Max Balls, no
    // buckets, or (adding up) a bucket that isn't a number; false and no error (quietly nothing) while it's off or the
    // last drop hasn't finished (balls falling, its result showing, its When Done waiting). onLand
    // gets each ball's bucket and its number in this drop as it lands; onDone every bucket (in the order they landed) and
    // the result, once the result has shown and gone
    public bool TryDrop(int count, Action<string, int>? onLand, Action<IReadOnlyList<string>, string>? onDone, out string error)
    {
        error = "";
        if (IsDisabled) return false;

        if (count < 1) error = $"{count} balls, it's 1 or more";
        else if (_maxBalls is { } max && count > max) error = $"{count} balls is over plinko '{Name}''s max of {max}";
        else if (Buckets.Count == 0) error = $"plinko '{Name}' has no buckets";
        else if (_mode == PlinkoMode.Sum && Buckets.FirstOrDefault(b => !TryNumber(b.Text, out _)) is { } bad)
            error = $"plinko '{Name}' adds up its buckets, '{bad.Text}' isn't a number";
        if (error != "" || IsBusy) return false;

        var drop = new Drop(count, onLand, onDone);
        for (var i = 1; i <= count; i++) _queue.Enqueue((drop, i));
        _settledAt = null;
        _alpha = 1;
        DropStarted?.Invoke();
        return true;
    }



    // moves the board on by that much time (the WidgetManager, 50 times a second; the tests). Off, nothing moves. Gives
    // how many balls hit a pin hard and how many drops finished (for the sounds)
    public (int PinHits, int Wins) Step(double seconds)
    {
        if (IsDisabled || !IsBusy) return (0, 0);

        var landed = new List<(Ball Ball, Action Callback)>();
        var pinHits = 0;
        var wins = 0;

        _carry = Math.Min(_carry + seconds, 0.1); // a long pause (a busy UI thread) doesn't fast-forward
        while (_carry >= StepSeconds)
        {
            _carry -= StepSeconds;
            StepOnce(landed, ref pinHits, ref wins);
        }

        // When Dones whose result has shown and gone; the result stops showing then too
        var due = _pendingDone.Where(d => d.Due <= _time).ToList();
        foreach (var done in due) _pendingDone.Remove(done);
        if (_result is { } result && _time - result.At >= _resultsSeconds + FadeSeconds) _result = null;

        // landed, all shown for the results time, then fading, then gone
        if (!IsDropping && _balls.Count > 0)
        {
            _settledAt ??= _time; // the steps above keep time moving while landed balls settle
            var since = _time - _settledAt.Value - _resultsSeconds;
            _alpha = since <= 0 ? 1 : Math.Max(0, 1 - since / FadeSeconds);
            if (_alpha <= 0) ClearBalls();
        }

        LiveChanged?.Invoke();
        foreach (var (_, callback) in landed) callback(); // after the step, so a block they run sees the board as it is
        foreach (var (_, run) in due) run();
        return (pinHits, wins);
    }



    // what's on the board now (the page and the preview draw it)
    public PlinkoFrame Frame() => new(_board, _balls.Select(b => new PlinkoBallState(b.Id, b.X, b.Y, b.Color)).ToList(), _alpha, _counts.ToArray(), ResultShowing());



    // the result as it fades in, shows for the Results Time, then fades out
    private PlinkoResult? ResultShowing()
    {
        if (_result is not { } result) return null;

        var t = _time - result.At;
        var alpha = Math.Min(1, t / FadeInSeconds) * Math.Clamp((_resultsSeconds + FadeSeconds - t) / FadeSeconds, 0, 1);
        return t < _resultsSeconds + FadeSeconds ? new(result.Text, result.Color, alpha) : null; // just landed counts (faded in 0)
    }



    // the physics



    private void StepOnce(List<(Ball, Action)> landed, ref int pinHits, ref int wins)
    {
        _time += StepSeconds;
        TrySpawn();

        var gravity = BaseGravity * _gravity;
        foreach (var ball in _balls)
        {
            ball.VY += gravity * StepSeconds;
            var speed = Math.Sqrt(ball.VX * ball.VX + ball.VY * ball.VY);
            if (speed > MaxSpeed)
            {
                ball.VX *= MaxSpeed / speed;
                ball.VY *= MaxSpeed / speed;
            }
            ball.X += ball.VX * StepSeconds;
            ball.Y += ball.VY * StepSeconds;
        }

        foreach (var ball in _balls)
        {
            if (ball.Bucket < 0) pinHits += CollidePins(ball);
            foreach (var segment in _board.Segments) CollideSegment(ball, segment);
        }

        if (_ballsCollide) CollideBalls();

        foreach (var ball in _balls.Where(b => b.Bucket < 0).ToList())
        {
            if (ball.Y - PlinkoBoard.BallR <= _board.BucketTop && _time - ball.Born < StuckSeconds) continue;
            if (Land(ball, landed)) wins++;
        }
    }



    // once Drop Tempo has passed, the next waiting ball gets a random spot in the drop-in area (picked once, never
    // again); it drops in there as soon as no ball is in the way, checked every step
    private void TrySpawn()
    {
        if (_queue.Count == 0 || _time - _lastSpawn < _dropTempo) return;

        _spawnAt ??= ((_random.NextDouble() * 2 - 1) * PlinkoBoard.SpawnHalfWidth, PlinkoBoard.SpawnY + (_random.NextDouble() * 2 - 1) * PlinkoBoard.SpawnHalfHeight);
        var (x, y) = _spawnAt.Value;
        var clear = Math.Pow(2 * PlinkoBoard.BallR + 0.05, 2);
        if (_balls.Any(b => (b.X - x) * (b.X - x) + (b.Y - y) * (b.Y - y) < clear)) return;

        var (drop, number) = _queue.Dequeue();
        _balls.Add(new Ball
        {
            Id = _nextId++, X = x, Y = y, VX = (_random.NextDouble() - 0.5) * 0.2, VY = 0,
            Color = WidgetOptionList.Palette[_random.Next(WidgetOptionList.Palette.Count)], Born = _time, Drop = drop, Number = number,
        });
        _spawnAt = null;
        _lastSpawn = _time;
    }



    // the pins near the ball (the rows around it, the pins around it in each, the wall's pins at the ends included); a
    // hard hit counts for the sound. Each hit nudges the ball a little sideways, so nothing balances on a pin
    private int CollidePins(Ball ball)
    {
        var hits = 0;
        var near = (int)Math.Round(ball.Y);
        for (var row = Math.Max(0, near - 1); row <= Math.Min(_board.Rows - 1, near + 1); row++)
        {
            var count = row + 3;
            var middle = (int)Math.Round(ball.X + (count - 1) / 2.0);
            for (var i = Math.Max(-1, middle - 1); i <= Math.Min(count, middle + 1); i++)
            {
                var impact = Bounce(ball, i - (count - 1) / 2.0, row, PlinkoBoard.PinR, nudge: true);
                if (impact > 1.5) hits++;
            }
        }
        return hits;
    }



    private void CollideSegment(Ball ball, PlinkoSegment s)
    {
        var dx = s.X2 - s.X1;
        var dy = s.Y2 - s.Y1;
        var t = Math.Clamp(((ball.X - s.X1) * dx + (ball.Y - s.Y1) * dy) / (dx * dx + dy * dy), 0, 1);
        Bounce(ball, s.X1 + t * dx, s.Y1 + t * dy, PlinkoBoard.SegmentHalfWidth, nudge: false);
    }



    // a ball against a fixed point of radius r: pushed out, its speed into it reflected by the bounciness. Gives how
    // hard it hit (0 if it didn't)
    private double Bounce(Ball ball, double x, double y, double r, bool nudge)
    {
        var dx = ball.X - x;
        var dy = ball.Y - y;
        var min = PlinkoBoard.BallR + r;
        var d2 = dx * dx + dy * dy;
        if (d2 >= min * min || d2 == 0) return 0;

        var d = Math.Sqrt(d2);
        var nx = dx / d;
        var ny = dy / d;
        ball.X = x + nx * min;
        ball.Y = y + ny * min;

        var into = ball.VX * nx + ball.VY * ny;
        if (into >= 0) return 0;

        ball.VX -= (1 + _bounciness) * into * nx;
        ball.VY -= (1 + _bounciness) * into * ny;
        if (nudge)
        {
            var kick = (_random.NextDouble() - 0.5) * 0.25 * -into;
            ball.VX += kick * -ny;
            ball.VY += kick * nx;
        }
        return -into;
    }



    // balls against each other (the same mass): only falling with falling, landed with landed (a pile doesn't block
    // what's still coming). Found through a grid, so many balls stay cheap
    private void CollideBalls()
    {
        var cell = 2 * PlinkoBoard.BallR;
        var grid = new Dictionary<(int, int), List<Ball>>();
        foreach (var ball in _balls)
        {
            var key = ((int)Math.Floor(ball.X / cell), (int)Math.Floor(ball.Y / cell));
            if (!grid.TryGetValue(key, out var list)) grid[key] = list = [];
            list.Add(ball);
        }

        foreach (var ((cx, cy), list) in grid)
            foreach (var a in list)
                for (var gx = cx - 1; gx <= cx + 1; gx++)
                    for (var gy = cy - 1; gy <= cy + 1; gy++)
                    {
                        if (!grid.TryGetValue((gx, gy), out var others)) continue;
                        foreach (var b in others)
                            if (a.Id < b.Id && (a.Bucket < 0) == (b.Bucket < 0)) Bounce(a, b);
                    }
    }



    private void Bounce(Ball a, Ball b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var min = 2 * PlinkoBoard.BallR;
        var d2 = dx * dx + dy * dy;
        if (d2 >= min * min || d2 == 0) return;

        var d = Math.Sqrt(d2);
        var nx = dx / d;
        var ny = dy / d;
        var push = (min - d) / 2;
        a.X -= nx * push;
        a.Y -= ny * push;
        b.X += nx * push;
        b.Y += ny * push;

        var closing = (b.VX - a.VX) * nx + (b.VY - a.VY) * ny;
        if (closing >= 0) return;

        var j = -(1 + _bounciness) * closing / 2;
        a.VX -= j * nx;
        a.VY -= j * ny;
        b.VX += j * nx;
        b.VY += j * ny;
    }



    // a ball is in a bucket: counted, its drop told (after the step), and a full bucket lets its oldest ball go. The
    // drop's last ball: its result shows, and its When Done waits for it to go. True if that finished a drop
    private bool Land(Ball ball, List<(Ball, Action)> landed)
    {
        var index = _board.BucketAt(ball.X);
        ball.Bucket = Math.Max(0, index);
        var text = index >= 0 ? _board.Buckets[index].Text : "";
        if (index >= 0 && index < _counts.Length) _counts[index]++;

        var drop = ball.Drop;
        drop.Results.Add(text);
        var number = ball.Number;
        var done = drop.Results.Count == drop.Count;
        landed.Add((ball, () => drop.OnLand?.Invoke(text, number)));

        if (done)
        {
            var results = drop.Results.ToList();
            var (resultText, color) = ResultOf(results);
            _lastResults = results;
            _lastResult = resultText;
            _result = (resultText, color, _time);
            _pendingDone.Add((_time + _resultsSeconds + FadeSeconds, () => drop.OnDone?.Invoke(results, resultText)));
        }

        if (index >= 0) LetOldestGo(index);
        return done;
    }



    // the most landed in (the first to get there on a tie, its box the bucket's colour), or the buckets added up (white)
    private (string Text, string Color) ResultOf(IReadOnlyList<string> results)
    {
        if (_mode == PlinkoMode.Sum)
        {
            var sum = results.Sum(r => TryNumber(r, out var n) ? n : 0);
            return (sum.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture), "#FFFFFF");
        }

        var counts = new Dictionary<string, int>();
        var best = "";
        foreach (var result in results)
        {
            counts[result] = counts.GetValueOrDefault(result) + 1;
            if (best == "" || counts[result] > counts[best]) best = result;
        }
        var bucket = _board.Buckets.FirstOrDefault(b => b.Text == best);
        return (best, bucket?.Color ?? "#FFFFFF");
    }



    private static bool TryNumber(string text, out double number) =>
        double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number);



    // a full bucket lets its oldest ball go (its count still counts it), so a big drop doesn't pile up into the pins
    private void LetOldestGo(int index)
    {
        var bucket = _board.Buckets[index];
        var depth = _board.Floor - _board.BucketTop;
        var capacity = Math.Max(1, (int)((bucket.Right - bucket.Left) * depth / Math.Pow(2 * PlinkoBoard.BallR, 2) * 0.6));
        var inBucket = _balls.Where(b => b.Bucket == index).ToList();
        if (inBucket.Count > capacity) _balls.Remove(inBucket[0]);
    }



    private void ClearBalls()
    {
        _balls.Clear();
        _spawnAt = null;
        _counts = new int[_board.Buckets.Count];
        _settledAt = null;
        _alpha = 1;
    }



    // helpers



    // the board after its rows or buckets changed; balls already on it carry on, landed ones counted where they are now
    private void RebuildBoard()
    {
        _board = PlinkoBoard.Build(_rows, Buckets);
        _counts = new int[_board.Buckets.Count];
        foreach (var ball in _balls.Where(b => b.Bucket >= 0))
        {
            ball.Bucket = Math.Max(0, _board.BucketAt(ball.X));
            if (ball.Bucket < _counts.Length) _counts[ball.Bucket]++;
        }
    }



    private bool TrySetNumber(double value, double min, double max, ref double field, string what)
    {
        if (!(value >= min && value <= max))
        {
            AppLog.WriteError($"can't set plinko {what} to {value}, it's {min} to {max}");
            return false;
        }

        if (value != field)
        {
            field = value;
            RaiseSettingsChanged();
        }
        return true;
    }



    private void SetText(ref string field, string value)
    {
        if (value == field) return;
        field = value;
        RaiseSettingsChanged();
    }



    // model callbacks



    private void OnBucketsChanged()
    {
        RebuildBoard();
        BucketsChanged?.Invoke();
        RaiseSettingsChanged();
    }



    // loaded: the board from the saved rows and buckets
    void IJsonOnDeserialized.OnDeserialized() => RebuildBoard();



    // disposal



    public override void Dispose()
    {
        _queue.Clear();
        _balls.Clear();
        _pendingDone.Clear();
        _buckets.Changed -= OnBucketsChanged;
        _buckets.Dispose();
    }
}
