using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamAssist.Core;

/// <summary>
/// Base class for all ViewModels. ViewLocator uses this to identify
/// which objects are ViewModels it should try to resolve a View for.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
