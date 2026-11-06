using System;

namespace StreamAssist.Components.Dashboard.Model;



// one row of an indicator or variable dock: just the name of what it shows (the state is kept elsewhere, by name:
// IndicatorsModel, the Data tab's variables, so two docks can show the same one). A row with no name is the growing
// list's empty one
public sealed class DockNameRow : IDisposable
{
    // private



    private string _name;



    // public



    public string Name => _name;
    public bool IsEmpty => _name == "";



    // events



    public event Action? NameChanged;



    // constructor



    public DockNameRow()
    {
        _name = "";
    }



    // public api



    public void SetName(string name)
    {
        if (name == _name) return;

        _name = name;
        NameChanged?.Invoke();
    }



    // disposal (nothing held)



    public void Dispose() { }
}
