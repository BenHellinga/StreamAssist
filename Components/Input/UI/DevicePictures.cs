using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;

using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.UI;



// a key of a picture: its label, code, place and size; a mouse's or controller's buttons also have their own corner
// rounding (shaped like the device) and a full name (what the editor's header says, the label can be an arrow)
public partial class KeyData(string Label, ushort VK, double X, double Y, double W, double H, string? Name = null, CornerRadius? Corners = null)
{
    public string Name { get; init; } = Name ?? Label;
    public CornerRadius? Corners { get; init; } = Corners;
    public string Label { get; init; } = Label;
    public ushort VK { get; init; } = VK;
    public double X { get; init; } = X;
    public double Y { get; init; } = Y;
    public double W { get; init; } = W;
    public double H { get; init; } = H;
}



// the pictures an input device box shows its keys in (a keyboard, every code in a grid, a mouse, a controller, a
// microphone's Talking button): each key's code, label, place and size at a key size, plus a mouse's or controller's
// body outline and how big a picture may get. The key names come from KeyNames
public static class DevicePictures
{
    // device inits



    public static (IReadOnlyList<KeyData>, double, double) InitKeyboardDisplayData(double keySize, double keyMargin)
    {
        double keyFull = keySize + keyMargin;
        double arrowSectionOffset = keyFull * 15 + keyMargin;
        double numpadSectionOffset = arrowSectionOffset + keyFull * 3 + keyMargin;

        double totalWidth = keySize * 22 + keyMargin * 23;
        double totalHeight = keySize * 6 + keyMargin * 5;

        var keyData = new List<(ushort vk, double x, double y, double w, double h)>
        {
            // function row
            (0x1b, 0, 0, keySize, keySize),
            (0x70, keyFull * 2, 0, keySize, keySize),
            (0x71, keyFull * 3, 0, keySize, keySize),
            (0x72, keyFull * 4, 0, keySize, keySize),
            (0x73, keyFull * 5, 0, keySize, keySize),
            (0x74, keyFull * 7 - keySize * 0.5, 0, keySize, keySize),
            (0x75, keyFull * 8 - keySize * 0.5, 0, keySize, keySize),
            (0x76, keyFull * 9 - keySize * 0.5, 0, keySize, keySize),
            (0x77, keyFull * 10 - keySize * 0.5, 0, keySize, keySize),
            (0x78, keyFull * 11, 0, keySize, keySize),
            (0x79, keyFull * 12, 0, keySize, keySize),
            (0x7a, keyFull * 13, 0, keySize, keySize),
            (0x7b, keyFull * 14, 0, keySize, keySize),
            (0x2c, arrowSectionOffset, 0, keySize, keySize),
            (0x91, arrowSectionOffset + keyFull, 0, keySize, keySize),
            (0x13, arrowSectionOffset + keyFull * 2, 0, keySize, keySize),

            // number row
            (0xc0, 0, keyFull, keySize, keySize),
            (0x31, keyFull, keyFull, keySize, keySize),
            (0x32, keyFull * 2, keyFull, keySize, keySize),
            (0x33, keyFull * 3, keyFull, keySize, keySize),
            (0x34, keyFull * 4, keyFull, keySize, keySize),
            (0x35, keyFull * 5, keyFull, keySize, keySize),
            (0x36, keyFull * 6, keyFull, keySize, keySize),
            (0x37, keyFull * 7, keyFull, keySize, keySize),
            (0x38, keyFull * 8, keyFull, keySize, keySize),
            (0x39, keyFull * 9, keyFull, keySize, keySize),
            (0x30, keyFull * 10, keyFull, keySize, keySize),
            (0xbd, keyFull * 11, keyFull, keySize, keySize),
            (0xbb, keyFull * 12, keyFull, keySize, keySize),
            (0x08, keyFull * 13, keyFull, keySize * 2 + keyMargin, keySize),
            (0x2d, arrowSectionOffset, keyFull, keySize, keySize),
            (0x24, arrowSectionOffset + keyFull, keyFull, keySize, keySize),
            (0x21, arrowSectionOffset + keyFull * 2, keyFull, keySize, keySize),
            (0x90, numpadSectionOffset, keyFull, keySize, keySize),
            (0x6f, numpadSectionOffset + keyFull, keyFull, keySize, keySize),
            (0x6a, numpadSectionOffset + keyFull * 2, keyFull, keySize, keySize),
            (0x6d, numpadSectionOffset + keyFull * 3, keyFull, keySize, keySize),

            // QWERTY row
            (0x09, 0, keyFull * 2, keySize * 1.5, keySize),
            (0x51, keySize * 1.5 + keyMargin, keyFull * 2, keySize, keySize),
            (0x57, keySize * 0.5 + keyFull * 2, keyFull * 2, keySize, keySize),
            (0x45, keySize * 0.5 + keyFull * 3, keyFull * 2, keySize, keySize),
            (0x52, keySize * 0.5 + keyFull * 4, keyFull * 2, keySize, keySize),
            (0x54, keySize * 0.5 + keyFull * 5, keyFull * 2, keySize, keySize),
            (0x59, keySize * 0.5 + keyFull * 6, keyFull * 2, keySize, keySize),
            (0x55, keySize * 0.5 + keyFull * 7, keyFull * 2, keySize, keySize),
            (0x49, keySize * 0.5 + keyFull * 8, keyFull * 2, keySize, keySize),
            (0x4f, keySize * 0.5 + keyFull * 9, keyFull * 2, keySize, keySize),
            (0x50, keySize * 0.5 + keyFull * 10, keyFull * 2, keySize, keySize),
            (0xdb, keySize * 0.5 + keyFull * 11, keyFull * 2, keySize, keySize),
            (0xdd, keySize * 0.5 + keyFull * 12, keyFull * 2, keySize, keySize),
            (0xdc, keySize * 0.5 + keyFull * 13, keyFull * 2, keySize * 1.5 + keyMargin, keySize),
            (0x2e, arrowSectionOffset, keyFull * 2, keySize, keySize),
            (0x23, arrowSectionOffset + keyFull, keyFull * 2, keySize, keySize),
            (0x22, arrowSectionOffset + keyFull * 2, keyFull * 2, keySize, keySize),
            (0x67, numpadSectionOffset, keyFull * 2, keySize, keySize),
            (0x68, numpadSectionOffset + keyFull, keyFull * 2, keySize, keySize),
            (0x69, numpadSectionOffset + keyFull * 2, keyFull * 2, keySize, keySize),
            (0x6b, numpadSectionOffset + keyFull * 3, keyFull * 2, keySize, keySize * 2 + keyMargin),

            // ASDF row
            (0x14, 0, keyFull * 3, keySize * 1.75, keySize),
            (0x41, keySize * 1.75 + keyMargin, keyFull * 3, keySize, keySize),
            (0x53, keySize * 0.75 + keyFull * 2, keyFull * 3, keySize, keySize),
            (0x44, keySize * 0.75 + keyFull * 3, keyFull * 3, keySize, keySize),
            (0x46, keySize * 0.75 + keyFull * 4, keyFull * 3, keySize, keySize),
            (0x47, keySize * 0.75 + keyFull * 5, keyFull * 3, keySize, keySize),
            (0x48, keySize * 0.75 + keyFull * 6, keyFull * 3, keySize, keySize),
            (0x4a, keySize * 0.75 + keyFull * 7, keyFull * 3, keySize, keySize),
            (0x4b, keySize * 0.75 + keyFull * 8, keyFull * 3, keySize, keySize),
            (0x4c, keySize * 0.75 + keyFull * 9, keyFull * 3, keySize, keySize),
            (0xba, keySize * 0.75 + keyFull * 10, keyFull * 3, keySize, keySize),
            (0xde, keySize * 0.75 + keyFull * 11, keyFull * 3, keySize, keySize),
            (0x0d, keySize * 0.75 + keyFull * 12, keyFull * 3, keySize * 2.25 + keyMargin * 2, keySize),
            (0x64, numpadSectionOffset, keyFull * 3, keySize, keySize),
            (0x65, numpadSectionOffset + keyFull, keyFull * 3, keySize, keySize),
            (0x66, numpadSectionOffset + keyFull * 2, keyFull * 3, keySize, keySize),

            // ZXCV row
            (0xa0, 0, keyFull * 4, keySize * 2.1 + keyMargin, keySize),
            (0x5a, keySize * 0.1 + keyFull * 2, keyFull * 4, keySize, keySize),
            (0x58, keySize * 0.1 + keyFull * 3, keyFull * 4, keySize, keySize),
            (0x43, keySize * 0.1 + keyFull * 4, keyFull * 4, keySize, keySize),
            (0x56, keySize * 0.1 + keyFull * 5, keyFull * 4, keySize, keySize),
            (0x42, keySize * 0.1 + keyFull * 6, keyFull * 4, keySize, keySize),
            (0x4e, keySize * 0.1 + keyFull * 7, keyFull * 4, keySize, keySize),
            (0x4d, keySize * 0.1 + keyFull * 8, keyFull * 4, keySize, keySize),
            (0xbc, keySize * 0.1 + keyFull * 9, keyFull * 4, keySize, keySize),
            (0xbe, keySize * 0.1 + keyFull * 10, keyFull * 4, keySize, keySize),
            (0xbf, keySize * 0.1 + keyFull * 11, keyFull * 4, keySize, keySize),
            (0xa1, keySize * 0.1 + keyFull * 12, keyFull * 4, keySize * 2.9 + keyMargin * 2, keySize),
            (0x26, arrowSectionOffset + keyFull, keyFull * 4, keySize, keySize),
            (0x61, numpadSectionOffset, keyFull * 4, keySize, keySize),
            (0x62, numpadSectionOffset + keyFull, keyFull * 4, keySize, keySize),
            (0x63, numpadSectionOffset + keyFull * 2, keyFull * 4, keySize, keySize),
            (0x0e, numpadSectionOffset + keyFull * 3, keyFull * 4, keySize, keySize * 2 + keyMargin),

            // bottom row
            (0xa2, 0, keyFull * 5, keySize * 1.1, keySize),
            (0x5B, keySize * 0.1 + keyFull * 2, keyFull * 5, keySize, keySize),
            (0xa4, keySize * 0.1 + keyFull * 3, keyFull * 5, keySize, keySize),
            (0x20, keySize * 0.1 + keyFull * 4, keyFull * 5, keySize * 6 + keyMargin * 5, keySize),
            (0xa5, keySize * 0.1 + keyFull * 10, keyFull * 5, keySize, keySize),
            (0x5d, keySize * 0.1 + keyFull * 12, keyFull * 5, keySize, keySize),
            (0xa3, keySize * 0.1 + keyFull * 13, keyFull * 5, keySize * 1.9 + keyMargin, keySize),
            (0x25, arrowSectionOffset, keyFull * 5, keySize, keySize),
            (0x28, arrowSectionOffset + keyFull, keyFull * 5, keySize, keySize),
            (0x27, arrowSectionOffset + keyFull * 2, keyFull * 5, keySize, keySize),
            (0x60, numpadSectionOffset, keyFull * 5, keySize * 2 + keyMargin, keySize),
            (0x6e, numpadSectionOffset + keyFull * 2, keyFull * 5, keySize, keySize),
        };

        var keys = new List<KeyData>();
        foreach (var (vk, x, y, w, h) in keyData)
            keys.Add(new(KeyNames.KeyboardLabel(vk)!, vk, x, y, w, h));

        return (keys, totalWidth, totalHeight);
    }



    // every code a keyboard, mouse or controller can send (0x01-0xFE) in a 32 by 8 grid of squares (about as wide for its height as the keyboard
    // picture), placed by its code: row = code / 32, column = code % 32 (0x7C, F13, is row 3 column 28). It fills the
    // slot's width like the picture does. Keys on a normal keyboard are labelled like on it, the rest by their windows
    // name a word per line (shrunk to fit), and one with no name its number (0x07), so an unusual key on a macro pad can
    // still be bound. Leaves out the either-side Shift / Ctrl / Alt codes (key presses are always one side)
    public static (IReadOnlyList<KeyData>, double, double) InitGenericDisplayKeys(double keySize, double keyMargin)
    {
        double keyFull = keySize + keyMargin;

        var keys = new List<KeyData>();
        for (ushort i = 0; i <= 0xff; ++i)
        {
            if (i is 0x00 or 0xFF or (>= 0x10 and <= 0x12)) continue;
            var label = GridLabel(i) ?? $"0x{i:X2}";

            keys.Add(new(label, i, (i % 32) * keyFull, (i / 32) * keyFull, keySize, keySize));
        }

        return (keys, 32 * keyFull - keyMargin, 8 * keyFull - keyMargin);
    }



    // a mouse (150 by 225 at key size 25): the body is BodyPath's outline (a seam under the buttons), on it the left and
    // right buttons (rounded like the body's top), the wheel between them (up, middle click, down, as pills), tilt left
    // and right under the seam, and the side buttons (forward above back) on the left
    public static (IReadOnlyList<KeyData>, double, double) InitMouseDisplayData(double keySize, double keyMargin)
    {
        var k = keySize / 25;
        KeyData Key(string label, string name, ushort code, double x, double y, double w, double h, double tl, double tr, double br, double bl) =>
            new(label, code, x * k, y * k, w * k, h * k, name, new CornerRadius(tl * k, tr * k, br * k, bl * k));

        var keys = new List<KeyData>
        {
            Key("Left", "Left", InputDevices.MouseLeft, 12, 12, 55, 86, 46, 4, 4, 4),
            Key("Right", "Right", InputDevices.MouseRight, 83, 12, 55, 86, 4, 46, 4, 4),
            Key("↑", "Wheel Up", InputDevices.WheelUp, 68, 20, 14, 16, 7, 7, 7, 7),
            Key("M", "Middle", InputDevices.MouseMiddle, 68, 39, 14, 22, 7, 7, 7, 7),
            Key("↓", "Wheel Down", InputDevices.WheelDown, 68, 64, 14, 16, 7, 7, 7, 7),
            Key("←", "Tilt Left", InputDevices.WheelLeft, 50, 116, 22, 14, 7, 7, 7, 7),
            Key("→", "Tilt Right", InputDevices.WheelRight, 78, 116, 22, 14, 7, 7, 7, 7),
            Key("Fwd", "Forward", InputDevices.MouseForward, 11, 128, 16, 32, 8, 8, 8, 8),
            Key("Back", "Back", InputDevices.MouseBack, 12, 164, 16, 32, 8, 8, 8, 8),
        };

        return (keys, 150 * k, 225 * k);
    }



    // an Xbox-style controller (350 by 215 at key size 25): the body is BodyPath's outline (two grips), triggers above
    // bumpers on its top edge, the left stick (round, its four directions around it) above the D-pad (a cross), ABXY
    // (round) above the right stick, View and Menu in the middle
    public static (IReadOnlyList<KeyData>, double, double) InitControllerDisplayData(double keySize, double keyMargin)
    {
        var k = keySize / 25;
        KeyData Key(string label, string name, ushort code, double x, double y, double w, double h, double tl, double tr, double br, double bl) =>
            new(label, code, x * k, y * k, w * k, h * k, name, new CornerRadius(tl * k, tr * k, br * k, bl * k));
        KeyData Round(string label, string name, ushort code, double x, double y, double size) =>
            Key(label, name, code, x, y, size, size, size / 2, size / 2, size / 2, size / 2);

        var keys = new List<KeyData>
        {
            Key("LT", "LT", InputDevices.PadLT, 66, 0, 46, 12, 6, 6, 2, 2),
            Key("LB", "LB", InputDevices.PadLB, 60, 15, 58, 11, 5, 5, 2, 2),
            Key("RT", "RT", InputDevices.PadRT, 238, 0, 46, 12, 6, 6, 2, 2),
            Key("RB", "RB", InputDevices.PadRB, 232, 15, 58, 11, 5, 5, 2, 2),

            Round("LS", "Left stick click", InputDevices.PadLS, 68, 55, 34),
            Key("↑", "Left stick up", InputDevices.LeftStickUp, 78, 40, 14, 12, 3, 3, 3, 3),
            Key("↓", "Left stick down", InputDevices.LeftStickDown, 78, 92, 14, 12, 3, 3, 3, 3),
            Key("←", "Left stick left", InputDevices.LeftStickLeft, 52, 65, 12, 14, 3, 3, 3, 3),
            Key("→", "Left stick right", InputDevices.LeftStickRight, 106, 65, 12, 14, 3, 3, 3, 3),

            Round("Y", "Y", InputDevices.PadY, 262, 44, 20),
            Round("A", "A", InputDevices.PadA, 262, 88, 20),
            Round("X", "X", InputDevices.PadX, 240, 66, 20),
            Round("B", "B", InputDevices.PadB, 284, 66, 20),

            Key("View", "View", InputDevices.PadView, 148, 66, 22, 13, 6, 6, 6, 6),
            Key("Menu", "Menu", InputDevices.PadMenu, 180, 66, 22, 13, 6, 6, 6, 6),

            Key("↑", "D-pad up", InputDevices.PadUp, 118, 106, 16, 18, 4, 4, 0, 0),
            Key("↓", "D-pad down", InputDevices.PadDown, 118, 140, 16, 18, 0, 0, 4, 4),
            Key("←", "D-pad left", InputDevices.PadLeft, 100, 124, 18, 16, 4, 0, 0, 4),
            Key("→", "D-pad right", InputDevices.PadRight, 134, 124, 18, 16, 0, 4, 4, 0),

            Round("RS", "Right stick click", InputDevices.PadRS, 208, 112, 34),
            Key("↑", "Right stick up", InputDevices.RightStickUp, 218, 97, 14, 12, 3, 3, 3, 3),
            Key("↓", "Right stick down", InputDevices.RightStickDown, 218, 149, 14, 12, 3, 3, 3, 3),
            Key("←", "Right stick left", InputDevices.RightStickLeft, 192, 122, 12, 14, 3, 3, 3, 3),
            Key("→", "Right stick right", InputDevices.RightStickRight, 246, 122, 12, 14, 3, 3, 3, 3),
        };

        return (keys, 350 * k, 215 * k);
    }



    // a microphone: one button, pressed while you're talking (its level meter and settings are under it, InputSlotView)
    public static (IReadOnlyList<KeyData>, double, double) InitMicrophoneDisplayData(double keySize)
    {
        var k = keySize / 25;
        return ([new("Talking", InputDevices.Talking, 0, 0, 150 * k, 40 * k, "Talking", new CornerRadius(8 * k))], 150 * k, 40 * k);
    }



    // a layout's picture: its keys, width and height at this key size
    public static (IReadOnlyList<KeyData>, double, double) DisplayData(string layout, double keySize, double keyMargin) => layout switch
    {
        InputSlotModel.AllKeysLayout => InitGenericDisplayKeys(keySize, keyMargin),
        InputSlotModel.MouseLayout => InitMouseDisplayData(keySize, keyMargin),
        InputSlotModel.ControllerLayout => InitControllerDisplayData(keySize, keyMargin),
        InputSlotModel.MicrophoneLayout => InitMicrophoneDisplayData(keySize),
        _ => InitKeyboardDisplayData(keySize, keyMargin),
    };



    // a device drawn with a body outline behind its buttons: its outline (and seams) as path data, at key size 25 (null:
    // no outline, keyboards are just their keys). Drawn as a line, scaled with the picture
    public static string? BodyPath(string layout) => layout switch
    {
        InputSlotModel.MouseLayout =>
            "M75,4 C116,4 146,34 146,92 L146,150 C146,198 116,221 75,221 C34,221 4,198 4,150 L4,92 C4,34 34,4 75,4 Z "
            + "M4,104 C40,111 110,111 146,104",
        InputSlotModel.ControllerLayout =>
            "M100,18 C130,12 220,12 250,18 C296,26 328,58 340,108 C352,160 346,204 316,210 C292,214 272,196 252,172 L98,172 "
            + "C78,196 58,214 34,210 C4,204 -2,160 10,108 C22,58 54,26 100,18 Z",
        _ => null,
    };



    // how much bigger than its key size a picture may be drawn: a small device stops growing (a mouse would otherwise
    // fill a wide box), keyboards fill the box's width
    public static double MaxScale(string layout) => layout switch
    {
        InputSlotModel.MouseLayout or InputSlotModel.ControllerLayout => 2,
        InputSlotModel.MicrophoneLayout => 1.5,
        _ => double.MaxValue,
    };



    // the keyboard label where it's clear (both Shifts are just "Shift" there, so sides use their windows name), else the
    // windows name with a word per line (MEDIA_PLAY_PAUSE → MEDIA / PLAY / PAUSE)
    private static string? GridLabel(ushort vk)
    {
        var sided = vk is >= 0xA0 and <= 0xA5 or 0x0E;
        if (!sided && KeyNames.KeyboardLabel(vk) is { } keyboardLabel) return keyboardLabel;
        return KeyNames.WindowsName(vk)?.Replace("_", "\n");
    }
}
