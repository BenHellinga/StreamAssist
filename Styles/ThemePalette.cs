using System.Collections.Generic;

namespace StreamAssist.Styles;



// the palette colors (Brushes.axaml) the Themes tab can change, and the brushes each one paints. Views use the brushes
// (mostly as StaticResource, and code keeps them too, e.g. the console's), so a change sets each brush's Color in place
// rather than swapping resources: everything already holding the brush redraws with it
public static class ThemePalette
{
    public static readonly IReadOnlyList<(string Key, string Label, string[] Brushes)> Colors =
    [
        ("ColorBackground", "Background", ["MainBackground"]),
        ("ColorBright", "Bright", ["TextBright"]),
        ("ColorText", "Text", ["TextNormal"]),
        ("ColorMuted", "Muted", ["TextMuted"]),
        ("ColorDim", "Dim", ["TextDim"]),
        ("ColorLine", "Line", ["PanelBorder", "Border", "Splitter"]),
        ("ColorGhost", "Ghost", ["Ghost"]),
        ("ColorSelected", "Selected", ["Accent", "PanelBorderHover", "PanelBorderToggle"]),
        ("ColorEnabled", "Enabled", ["Bound"]),
        ("ColorDisabled", "Disabled", ["Disabled"]),
    ];
}
