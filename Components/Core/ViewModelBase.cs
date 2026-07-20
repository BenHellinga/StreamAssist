using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamAssist.Components.Core;



public abstract class ViewModelBase : ObservableObject, IDisposable
{
    public abstract void Dispose();
}
