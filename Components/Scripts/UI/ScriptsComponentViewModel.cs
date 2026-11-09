using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Scripts.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;
using StreamAssist.Shell;

namespace StreamAssist.Components.Scripts.UI;



// the Scripts tab: the config bar, then the config in use's scripts under a Scripts title, a name box and a bind box
// each (the last row is the empty one: typing a name adds a script). Clicking a bind box opens its script in the
// bottom editor. Dragging a script's binding onto another row moves the whole script before that one
public partial class ScriptsComponentViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly ScriptsComponentModel _model;
    private readonly BindableEditorViewModel _editor; // lives in the bottom panel, which disposes it
    private readonly ConfigBarViewModel _configBar;
    private ScriptsConfigModel? _config; // the one shown
    private ScriptModel? _selected; // the one open in the editor

    [ObservableProperty] private bool _hasConfig;



    // public



    public ConfigBarViewModel ConfigBar => _configBar;
    public NoConfigViewModel NoConfig { get; } = new(); // shown instead of the scripts while there's no config
    public ObservableCollection<ScriptRowViewModel> Rows { get; }



    // activator



    public static ScriptsComponentViewModel ActivateTab(ScriptsComponentModel model, WindowLayoutModel layout, PanelViewModel sidePanel, PanelViewModel bottomPanel)
    {
        var editor = new BindableEditorViewModel(layout, "No script selected");
        sidePanel.TransferContent(null);
        bottomPanel.TransferContent(editor);
        return new(model, editor);
    }



    // constructor



    public ScriptsComponentViewModel(ScriptsComponentModel model, BindableEditorViewModel editor)
    {
        _model = model;
        _editor = editor;
        _configBar = new(_model.Configs);
        Rows = [];

        ScriptSelection.Set(null); // a fresh tab starts with nothing open
        ShowConfig();

        _model.Configs.ListChanged += OnConfigsChanged;
        ScriptSelection.Changed += OnSelectionChanged;
    }



    // drag drop: only reordering, the binding of one of the shown scripts



    public bool CanAcceptDropPayload(object payload) => FindScript(payload) != null;



    public bool PassesOn(object payload) => !CanAcceptDropPayload(payload);



    // zoneContext is the row it was dropped on, it goes before that one
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (FindScript(payload) is not { } script || zoneContext is not ScriptRowViewModel before) return;
        _config?.TryMoveScript(script, Rows.IndexOf(before));
    }



    // model events



    private void OnConfigsChanged()
    {
        if (_model.Config != _config) ShowConfig();
    }



    private void OnScriptInserted(ScriptModel script, int index) => Rows.Insert(index, new ScriptRowViewModel(script, this));



    private void OnScriptMoved(ScriptModel _, int from, int to) => Rows.Move(from, to);



    private void OnScriptRemoved(ScriptModel script)
    {
        if (script == _selected) ScriptSelection.Set(null);

        for (var i = 0; i < Rows.Count; i++)
        {
            if (!Rows[i].Represents(script)) continue;
            Rows[i].Dispose();
            Rows.RemoveAt(i);
            return;
        }
    }



    private void OnSelectionChanged()
    {
        if (_selected != null)
        {
            _selected.BindableChanged -= OnSelectedBindableChanged;
            _selected.NameChanged -= OnSelectedNameChanged;
        }

        _editor.ClearBindable();
        _selected = ScriptSelection.Selected;
        if (_selected == null) return;

        _selected.BindableChanged += OnSelectedBindableChanged;
        _selected.NameChanged += OnSelectedNameChanged;
        ShowSelected();
    }



    // bound or unbound from somewhere else (a binding dropped on it, its blocks all removed); our own draft being
    // saved is the editor's binding already, so it keeps editing it
    private void OnSelectedBindableChanged()
    {
        if (_selected == null || _selected.Bindable == _editor.Bindable) return;
        ShowSelected();
    }



    private void OnSelectedNameChanged()
    {
        if (_selected != null) _editor.Label = Label(_selected);
    }



    // helpers



    // the config in use's scripts (none: the "create a config" page), with no script open
    private void ShowConfig()
    {
        if (_config != null)
        {
            _config.ScriptInserted -= OnScriptInserted;
            _config.ScriptRemoved -= OnScriptRemoved;
            _config.ScriptMoved -= OnScriptMoved;
        }

        ScriptSelection.Set(null);
        foreach (var row in Rows) row.Dispose();
        Rows.Clear();

        _config = _model.Config;
        HasConfig = _config != null;
        if (_config == null) return;

        foreach (var script in _config.Scripts) Rows.Add(new ScriptRowViewModel(script, this));
        _config.ScriptInserted += OnScriptInserted;
        _config.ScriptRemoved += OnScriptRemoved;
        _config.ScriptMoved += OnScriptMoved;
    }



    // the script's binding, or a fresh empty draft (bound to it on its first change) if it has none
    private void ShowSelected()
    {
        if (_selected == null) return;

        if (_selected.Bindable != null) _editor.ShowBindable(_selected.Bindable, Label(_selected));
        else _editor.ShowDraft(Label(_selected), _selected.TryBind, startWithOnPressed: false); // a script just runs once
    }



    private static string Label(ScriptModel script) => $"Script: {script.Name}";



    private ScriptModel? FindScript(object payload) =>
        payload is BindableDragPayload dragged ? _config?.Scripts.FirstOrDefault(s => s.Bindable == dragged.Bindable) : null;



    // disposal



    public override void Dispose()
    {
        _model.Configs.ListChanged -= OnConfigsChanged;
        ScriptSelection.Changed -= OnSelectionChanged;
        if (_config != null)
        {
            _config.ScriptInserted -= OnScriptInserted;
            _config.ScriptRemoved -= OnScriptRemoved;
            _config.ScriptMoved -= OnScriptMoved;
        }

        ScriptSelection.Set(null); // runs OnSelectionChanged no more, so let go of the script here
        if (_selected != null)
        {
            _selected.BindableChanged -= OnSelectedBindableChanged;
            _selected.NameChanged -= OnSelectedNameChanged;
            _selected = null;
        }
        _editor.ClearBindable();

        foreach (var row in Rows) row.Dispose();
        _configBar.Dispose();
    }
}
