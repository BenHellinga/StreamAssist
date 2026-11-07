using StreamAssist.Components.Blocks.Model;

namespace StreamAssist.Tests;



// picks a block's dropdown option in a check, by the dropdown's old property name (its label without spaces, or the
// label for the few that differ: Change -> Do, Anchor -> Keep Fixed)
internal static class MenuPicks
{
    public static T Pick<T>(this T block, string name, string option) where T : BlockModel
    {
        var slot = Find(block, name) ?? throw new InvalidOperationException($"{block.Name} has no dropdown '{name}'");
        slot.SetText(option);
        return block;
    }



    // the option a dropdown has picked (its typed text)
    public static string Picked(this BlockModel block, string name)
    {
        return Find(block, name)!.Text;
    }



    private static MenuSlot? Find(BlockModel block, string name)
    {
        var menus = block.Slots.OfType<MenuSlot>().ToList();
        var other = name switch { "Change" => "Do", "Anchor" => "Keep Fixed", _ => name };
        return menus.FirstOrDefault(s => s.Label.Replace(" ", "") == name) ?? menus.FirstOrDefault(s => s.Label == other);
    }
}
