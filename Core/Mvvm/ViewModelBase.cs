using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamAssist.Core.Mvvm;



public abstract class ViewModelBase : ObservableObject, IDisposable
{
    public abstract void Dispose();
}
