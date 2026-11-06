using System;
using System.Text.Json.Serialization;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Components.OutputConsole.Model;

namespace StreamAssist.Components.Dashboard.Model;



// one row of a button dock: its name, and the binding it runs when pressed (like a redeem row's). A row with no name
// and no binding is the growing list's empty one
public sealed class DockButton : IDisposable
{
    // private



    private string _name;
    private BindableModel? _bindable;



    // public



    public string Name => _name;
    public BindableModel? Bindable => _bindable;
    public bool IsEmpty => _name == "" && _bindable == null;



    // events



    public event Action? NameChanged;
    public event Action? BindableChanged; // bound, unbound, or a different binding
    public event Action? Changed; // anything saved changed (the name, the binding or its blocks)



    // constructor



    public DockButton()
    {
        _name = "";
        _bindable = null;
    }



    // public api



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        NameChanged?.Invoke();
        Changed?.Invoke();
    }



    public bool TryBind(BindableModel bindable)
    {
        if (_bindable != null)
        {
            AppLog.WriteError($"can't bind button '{_name}', already bound");
            return false;
        }

        Bind(bindable);
        return true;
    }



    // the binding goes (and is disposed, which stops its runs)
    public void Unbind()
    {
        if (_bindable == null) return;

        var old = _bindable;
        _bindable = null;
        old.Changed -= OnBindableChanged;
        old.AttachUnbindSelf(null);

        BindableChanged?.Invoke();
        Changed?.Invoke();
        old.Dispose();
    }



    // pressed: runs the binding, false if there's none or it's disabled (nothing to report)
    public bool TryRun()
    {
        if (_bindable == null || _bindable.IsDisabled) return false;

        _bindable.TriggerOnce();
        return true;
    }



    // helpers



    private void Bind(BindableModel bindable)
    {
        _bindable = bindable;
        _bindable.AttachUnbindSelf(Unbind); // a binding whose blocks are all removed goes away, like an empty key
        _bindable.Changed += OnBindableChanged;

        BindableChanged?.Invoke();
        Changed?.Invoke();
    }



    private void OnBindableChanged() => Changed?.Invoke();



    // json



    public DockButtonData GetData() => new() { Name = _name, Bindable = _bindable };



    public void LoadData(DockButtonData data)
    {
        _name = data.Name;
        if (data.Bindable == null) return;

        if (data.Bindable.HasBlock) Bind(data.Bindable);
        else data.Bindable.Dispose();
    }



    // disposal



    public void Dispose()
    {
        if (_bindable == null) return;

        _bindable.Changed -= OnBindableChanged;
        _bindable.Dispose();
    }
}



// how a button row is saved
public class DockButtonData
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("bindable")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BindableModel? Bindable { get; set; }
}
