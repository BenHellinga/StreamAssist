using System.Collections.Generic;
using System.Linq;

namespace StreamAssist.Components.OBS.Model;



// one number of a source's place in a scene that the Get / Set OBS Transform blocks offer: its name in the dropdown
// and its key in obs-websocket's sceneItemTransform. Width and Height aren't stored by OBS (they're the source's size
// times its scale), so setting them sets the scale instead
public sealed record ObsTransform(string Name, string Key, string Description);



public static class ObsTransforms
{
    public static readonly IReadOnlyList<ObsTransform> All =
    [
        new("Position X", "positionX", "pixels from the left of the canvas"),
        new("Position Y", "positionY", "pixels from the top of the canvas"),
        new("Width", "width", "its width on screen in pixels (setting it changes Scale X)"),
        new("Height", "height", "its height on screen in pixels (setting it changes Scale Y)"),
        new("Scale X", "scaleX", "1 = its own size"),
        new("Scale Y", "scaleY", "1 = its own size"),
        new("Rotation", "rotation", "degrees clockwise"),
        new("Crop Left", "cropLeft", "pixels cut off the left"),
        new("Crop Right", "cropRight", "pixels cut off the right"),
        new("Crop Top", "cropTop", "pixels cut off the top"),
        new("Crop Bottom", "cropBottom", "pixels cut off the bottom"),
    ];



    public static IReadOnlyList<string> Names => All.Select(t => t.Name).ToList();



    public static ObsTransform? Find(string name) => All.FirstOrDefault(t => t.Name == name);
}
