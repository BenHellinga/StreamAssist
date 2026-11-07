using System.Collections.Generic;
using StreamAssist.Components.Blocks.Model;
using StreamAssist.Shell;

namespace StreamAssist.Components.Widgets.Model;



// what every widget block shares: its Widget dropdown (the config in use's widgets of its kind, a typed or block-given
// name works too) and finding the widget by it, like dock blocks find their dock
public static class WidgetBlocks
{
    public static MenuSlot WidgetSlot(WidgetKind? kind) => new("Widget", () => Names(kind), "", isFixed: false);



    public static List<string> Names(WidgetKind? kind) => GlobalContext.WidgetsComponentModel?.WidgetNames(kind) ?? [];



    // the widget the slot names, or the reason for the block to fail with
    public static bool TryFind(string name, WidgetKind? kind, out WidgetModel widget, out string error)
    {
        widget = null!;
        if (GlobalContext.WidgetsComponentModel is not { } widgets)
        {
            error = "widgets not set up";
            return false;
        }

        return widgets.TryFindWidget(name, kind, out widget, out error);
    }
}
