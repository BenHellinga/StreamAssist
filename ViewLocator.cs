using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using StreamAssist.Core.Mvvm;

namespace StreamAssist;

/// <summary>
/// Avalonia's ViewLocator convention: given a ViewModel, find its View by
/// replacing "ViewModel" with "View" in the fully-qualified type name.
///
/// For example:
///   StreamAssist.Components.Keyboard.UI.KeyViewModel
///   → StreamAssist.Components.Keyboard.UI.KeyView  ✓
///
/// So the convention works automatically as long as your View and ViewModel
/// live in the same namespace (same folder).
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
