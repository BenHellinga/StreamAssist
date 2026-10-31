using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Shell;



// how the window was left: Minimized is never saved, the window reopens in its last visible state
public enum WindowMode { Normal, Maximized, FullScreen }



// window size/position/state, selected tab and splitter positions, saved to window.json
public class WindowLayoutModel : ModelBase
{
    // panel keys (each resizable panel saves its size under one of these)



    public const string OutputConsolePanel = "outputConsole";
    public const string SidePanel = "sidePanel";
    public const string BottomPanel = "bottomPanel";
    public const string BlockListPanel = "blockList";
    public const string ChatBlockListPanel = "chatBlockList";



    // private



    // position is in screen pixels, size in window units (what Width/Height use); NaN means "not saved yet"
    private double _left;
    private double _top;
    private double _width;
    private double _height;
    private WindowMode _mode;
    private string? _selectedTab;
    private Dictionary<string, double> _panelSizes;
    private List<string> _collapsedBlockGroups;



    // public



    [JsonIgnore] public double Left => _left;
    [JsonIgnore] public double Top => _top;
    [JsonIgnore] public double Width => _width;
    [JsonIgnore] public double Height => _height;
    [JsonIgnore] public bool HasPosition => !double.IsNaN(_left) && !double.IsNaN(_top);
    [JsonIgnore] public bool HasSize => !double.IsNaN(_width) && !double.IsNaN(_height);
    [JsonIgnore] public WindowMode Mode => _mode;
    [JsonIgnore] public string? SelectedTab => _selectedTab;



    // json



    [JsonInclude] [JsonPropertyName("left")]
    private double? JsonLeft
    {
        get => double.IsNaN(_left) ? null : _left;
        set => _left = value ?? double.NaN;
    }

    [JsonInclude] [JsonPropertyName("top")]
    private double? JsonTop
    {
        get => double.IsNaN(_top) ? null : _top;
        set => _top = value ?? double.NaN;
    }

    [JsonInclude] [JsonPropertyName("width")]
    private double? JsonWidth
    {
        get => double.IsNaN(_width) ? null : _width;
        set => _width = value ?? double.NaN;
    }

    [JsonInclude] [JsonPropertyName("height")]
    private double? JsonHeight
    {
        get => double.IsNaN(_height) ? null : _height;
        set => _height = value ?? double.NaN;
    }

    [JsonInclude] [JsonPropertyName("mode")] [JsonConverter(typeof(JsonStringEnumConverter))]
    private WindowMode JsonMode
    {
        get => _mode;
        set => _mode = value;
    }

    [JsonInclude] [JsonPropertyName("selectedTab")]
    private string? JsonSelectedTab
    {
        get => _selectedTab;
        set => _selectedTab = value;
    }

    [JsonInclude] [JsonPropertyName("panelSizes")]
    private Dictionary<string, double> JsonPanelSizes
    {
        get => _panelSizes;
        set => _panelSizes = value;
    }

    [JsonInclude] [JsonPropertyName("collapsedBlockGroups")]
    private List<string> JsonCollapsedBlockGroups
    {
        get => _collapsedBlockGroups;
        set => _collapsedBlockGroups = value;
    }



    // events



    public event Action? BoundsChanged;
    public event Action<WindowMode>? ModeChanged;
    public event Action<string?>? SelectedTabChanged;
    public event Action<string, double>? PanelSizeChanged;
    public event Action<string>? BlockGroupCollapsedChanged; // a block list heading was collapsed or expanded (its name)
    public event Action? Changed; // any of the above (used for saving)



    // constructor



    public WindowLayoutModel()
    {
        _left = double.NaN;
        _top = double.NaN;
        _width = double.NaN;
        _height = double.NaN;
        _mode = WindowMode.Normal;
        _selectedTab = null;
        _panelSizes = [];
        _collapsedBlockGroups = [];
    }



    // window



    // the size/position the window has when it isn't maximized or fullscreen
    public void SetBounds(double left, double top, double width, double height)
    {
        if (left == _left && top == _top && width == _width && height == _height) return;

        _left = left;
        _top = top;
        _width = width;
        _height = height;

        BoundsChanged?.Invoke();
        Changed?.Invoke();
    }



    public void SetMode(WindowMode mode)
    {
        if (mode == _mode) return;

        _mode = mode;
        ModeChanged?.Invoke(_mode);
        Changed?.Invoke();
    }



    // tabs



    public void SetSelectedTab(string? label)
    {
        if (label == _selectedTab) return;

        _selectedTab = label;
        SelectedTabChanged?.Invoke(_selectedTab);
        Changed?.Invoke();
    }



    // panels



    public double GetPanelSize(string panel, double fallback) =>
        _panelSizes.TryGetValue(panel, out var size) ? size : fallback;



    public void SetPanelSize(string panel, double size)
    {
        if (size <= 0) return; // a hidden panel has size 0, that's not something to remember
        if (_panelSizes.TryGetValue(panel, out var current) && current == size) return;

        _panelSizes[panel] = size;
        PanelSizeChanged?.Invoke(panel, size);
        Changed?.Invoke();
    }



    // block list headings (by name, e.g. "Twitch"), collapsed ones show only their heading



    public bool IsBlockGroupCollapsed(string group) => _collapsedBlockGroups.Contains(group);



    public void SetBlockGroupCollapsed(string group, bool collapsed)
    {
        if (collapsed == IsBlockGroupCollapsed(group)) return;

        if (collapsed) _collapsedBlockGroups.Add(group);
        else _collapsedBlockGroups.Remove(group);

        BlockGroupCollapsedChanged?.Invoke(group);
        Changed?.Invoke();
    }



    // disposal



    public override void Dispose() {}
}
