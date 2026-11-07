using System.Collections.ObjectModel;
using System.Linq;
using StreamAssist.Components.Blocks.UI;
using StreamAssist.Components.Input.Model;
using StreamAssist.Components.Widgets.Model;
using StreamAssist.Core.Dragging;
using StreamAssist.Core.Mvvm;

namespace StreamAssist.Components.Widgets.UI;



// the Widgets tab's widget boxes, top to bottom (the config in use's). A widget card dropped on a box adds one before it
// (on the drop zone at the end, last); a box dragged by its grip moves the same way; a copied widget pastes onto the
// drop zone at the end
public partial class WidgetListViewModel : ViewModelBase, IDroppable
{
    // private



    private readonly WidgetsComponentModel _model;
    private readonly InputListModel? _inputs;
    private readonly ConfigBarViewModel _configBar;
    private WidgetsConfigModel? _config; // the one shown



    // public



    public ObservableCollection<WidgetViewModel> Widgets { get; }
    public ConfigBarViewModel ConfigBar => _configBar; // one config for the whole tab
    public NoConfigViewModel NoConfig { get; } = new(); // shown instead of the widgets while there's no config
    public bool HasConfig => _config != null;
    public bool HasWidgets => Widgets.Count > 0; // the empty list is hidden, so the stack doesn't space it from the drop zone



    // a copied widget pastes as a WidgetCopy (BlockClipboard)
    static WidgetListViewModel()
    {
        BlockClipboard.RegisterKind(WidgetCopy.Kind, data => System.Text.Json.JsonSerializer.Deserialize<WidgetModel>(data) is { } widget ? new WidgetCopy(widget) : null);
    }



    // constructor



    public WidgetListViewModel(WidgetsComponentModel model, InputListModel? inputs)
    {
        _model = model;
        _inputs = inputs;
        _configBar = new(_model.Configs);
        Widgets = [];

        ShowConfig();
        _model.Configs.ListChanged += OnConfigsChanged;
        Widgets.CollectionChanged += OnWidgetsChanged;
    }



    // drag drop



    public bool CanAcceptDropPayload(object payload) =>
        _config != null && (payload is NewWidgetPayload || payload is WidgetCopy
            || payload is WidgetDragPayload dragged && _config.Widgets.Contains(dragged.Widget));



    // zoneContext is the box it was dropped on (it goes before that one), or nothing for the zone at the end
    public void OnDroppedOn(object payload, object? zoneContext)
    {
        if (_config == null) return;

        var index = zoneContext is WidgetViewModel before ? Widgets.IndexOf(before) : Widgets.Count;
        if (index < 0) index = Widgets.Count;

        if (payload is NewWidgetPayload card) _config.TryAddWidget(card.Kind, index, out _);
        else if (payload is WidgetDragPayload dragged) _config.TryMoveWidget(dragged.Widget, index);
        else if (payload is WidgetCopy copy) _config.TryPasteWidget(copy.Widget, index);
    }



    // model events



    private void OnConfigsChanged()
    {
        if (_model.Config != _config) ShowConfig();
    }



    private void OnWidgetAdded(WidgetModel widget, int index) => Widgets.Insert(index, new WidgetViewModel(_model, _config!, widget, _inputs));



    private void OnWidgetRemoved(WidgetModel widget)
    {
        if (Widgets.FirstOrDefault(w => w.RepresentsModel(widget)) is not { } viewModel) return;

        Widgets.Remove(viewModel);
        viewModel.Dispose();
    }



    private void OnWidgetMoved(WidgetModel _, int from, int to) => Widgets.Move(from, to);



    private void OnWidgetsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => OnPropertyChanged(nameof(HasWidgets));



    // helpers



    // the config in use's widgets (none: the "create a config" page)
    private void ShowConfig()
    {
        if (_config != null)
        {
            _config.WidgetAdded -= OnWidgetAdded;
            _config.WidgetRemoved -= OnWidgetRemoved;
            _config.WidgetMoved -= OnWidgetMoved;
        }

        foreach (var widget in Widgets) widget.Dispose();
        Widgets.Clear();

        _config = _model.Config;
        OnPropertyChanged(nameof(HasConfig));
        if (_config == null) return;

        foreach (var widget in _config.Widgets) Widgets.Add(new WidgetViewModel(_model, _config, widget, _inputs));
        _config.WidgetAdded += OnWidgetAdded;
        _config.WidgetRemoved += OnWidgetRemoved;
        _config.WidgetMoved += OnWidgetMoved;
    }



    // disposal



    public override void Dispose()
    {
        _model.Configs.ListChanged -= OnConfigsChanged;
        Widgets.CollectionChanged -= OnWidgetsChanged;
        if (_config != null)
        {
            _config.WidgetAdded -= OnWidgetAdded;
            _config.WidgetRemoved -= OnWidgetRemoved;
            _config.WidgetMoved -= OnWidgetMoved;
        }

        foreach (var widget in Widgets) widget.Dispose();
        _configBar.Dispose();
    }
}
