using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using System.Collections.Generic;

using StreamAssist.Components.Input.Background;
using StreamAssist.Components.Input.Model;

namespace StreamAssist.Components.Input.UI;



// TODO awaiting reworking once i generalize display key and config to work with other tabs
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



// TODO awaiting reworking once i generalize display key and config to work with other tabs
public static class DeviceDisplayHelper
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
            keys.Add(new(VKToKeyboardLabel(vk)!, vk, x, y, w, h));

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
        if (!sided && VKToKeyboardLabel(vk) is { } keyboardLabel) return keyboardLabel;
        return VKToGenericLabel(vk)?.Replace("_", "\n");
    }



    // name -> key code



    // common names people type that aren't the windows names (CONTROL, MENU, RETURN, ...)
    private static readonly Dictionary<string, ushort> KeyNameAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11, ["alt"] = 0x12, ["win"] = 0x5b, ["windows"] = 0x5b,
        ["enter"] = 0x0d, ["esc"] = 0x1b, ["backspace"] = 0x08, ["del"] = 0x2e, ["ins"] = 0x2d,
        ["caps"] = 0x14, ["capslock"] = 0x14, ["pgup"] = 0x21, ["pageup"] = 0x21, ["pgdn"] = 0x22, ["pagedown"] = 0x22,
        ["menu"] = 0x5d, ["prtsc"] = 0x2c, ["printscreen"] = 0x2c, ["numlock"] = 0x90, ["scrolllock"] = 0x91,
    };



    // turns what the user typed into a key code: an alias ("ctrl"), a windows name ("SHIFT", "F5", "NUMPAD1"),
    // or a character printed on the key (";", "[", "/"), not case sensitive
    public static bool TryParseKeyName(string? text, out ushort vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var name = text.Trim();

        if (KeyNameAliases.TryGetValue(name, out vk)) return true;

        for (ushort i = 0; i <= 0xff; ++i)
        {
            if (string.Equals(VKToGenericLabel(i), name, StringComparison.OrdinalIgnoreCase))
            {
                vk = i;
                return true;
            }
        }

        // main keys are checked before the numpad, so "/" means the slash key, not numpad divide
        // (numpad keys can still be typed by name, e.g. "DIVIDE" or "NUMPAD1")
        foreach (var checkNumpad in new[] { false, true })
        {
            for (ushort i = 0; i <= 0xff; ++i)
            {
                var isNumpad = i >= 0x60 && i <= 0x6f;
                if (isNumpad != checkNumpad) continue;

                var label = VKToKeyboardLabel(i);
                if (label == null) continue;

                foreach (var part in label.Split('\n'))
                {
                    if (!string.Equals(part, name, StringComparison.OrdinalIgnoreCase)) continue;

                    vk = i;
                    return true;
                }
            }
        }

        return false;
    }



    // label maps



    public static string? VKToGenericLabel(ushort vk)
    {
        switch (vk)
        {
            case 0x01: return "LBUTTON";
            case 0x02: return "RBUTTON";
            case 0x03: return "CANCEL";
            case 0x04: return "MBUTTON";
            case 0x05: return "XBUTTON1";
            case 0x06: return "XBUTTON2";
            case 0x08: return "BACK";
            case 0x3a: return "WHEEL_UP"; // not windows codes, the app's own for the mouse wheel
            case 0x3b: return "WHEEL_DOWN";
            case 0x3c: return "WHEEL_LEFT";
            case 0x3d: return "WHEEL_RIGHT";
            case 0x09: return "TAB";
            case 0x0c: return "CLEAR";
            case 0x0e: return "NUMPADENTER"; // not a windows code, the app's own for numpad Enter
            case 0x0d: return "RETURN";
            case 0x10: return "SHIFT";
            case 0x11: return "CONTROL";
            case 0x12: return "MENU";
            case 0x13: return "PAUSE";
            case 0x14: return "CAPITAL";
            case 0x15: return "KANA/HANGUL";
            case 0x16: return "IME_ON";
            case 0x17: return "JUNJA";
            case 0x18: return "FINAL";
            case 0x19: return "HANJA/KANJI";
            case 0x1a: return "IME_OFF";
            case 0x1b: return "ESCAPE";
            case 0x1c: return "CONVERT";
            case 0x1d: return "NONCONVERT";
            case 0x1e: return "ACCEPT";
            case 0x1f: return "MODECHANGE";
            case 0x20: return "SPACE";
            case 0x21: return "PRIOR";
            case 0x22: return "NEXT";
            case 0x23: return "END";
            case 0x24: return "HOME";
            case 0x25: return "LEFT";
            case 0x26: return "UP";
            case 0x27: return "RIGHT";
            case 0x28: return "DOWN";
            case 0x29: return "SELECT";
            case 0x2a: return "PRINT";
            case 0x2b: return "EXECUTE";
            case 0x2c: return "SNAPSHOT";
            case 0x2d: return "INSERT";
            case 0x2e: return "DELETE";
            case 0x2f: return "HELP";
            case 0x30: return "0";
            case 0x31: return "1";
            case 0x32: return "2";
            case 0x33: return "3";
            case 0x34: return "4";
            case 0x35: return "5";
            case 0x36: return "6";
            case 0x37: return "7";
            case 0x38: return "8";
            case 0x39: return "9";
            case 0x41: return "A";
            case 0x42: return "B";
            case 0x43: return "C";
            case 0x44: return "D";
            case 0x45: return "E";
            case 0x46: return "F";
            case 0x47: return "G";
            case 0x48: return "H";
            case 0x49: return "I";
            case 0x4a: return "J";
            case 0x4b: return "K";
            case 0x4c: return "L";
            case 0x4d: return "M";
            case 0x4e: return "N";
            case 0x4f: return "O";
            case 0x50: return "P";
            case 0x51: return "Q";
            case 0x52: return "R";
            case 0x53: return "S";
            case 0x54: return "T";
            case 0x55: return "U";
            case 0x56: return "V";
            case 0x57: return "W";
            case 0x58: return "X";
            case 0x59: return "Y";
            case 0x5a: return "Z";
            case 0x5b: return "LWIN";
            case 0x5c: return "RWIN";
            case 0x5d: return "APPS";
            case 0x5f: return "SLEEP";
            case 0x60: return "NUMPAD0";
            case 0x61: return "NUMPAD1";
            case 0x62: return "NUMPAD2";
            case 0x63: return "NUMPAD3";
            case 0x64: return "NUMPAD4";
            case 0x65: return "NUMPAD5";
            case 0x66: return "NUMPAD6";
            case 0x67: return "NUMPAD7";
            case 0x68: return "NUMPAD8";
            case 0x69: return "NUMPAD9";
            case 0x6a: return "MULTIPLY";
            case 0x6b: return "ADD";
            case 0x6c: return "SEPARATOR";
            case 0x6d: return "SUBTRACT";
            case 0x6e: return "DECIMAL";
            case 0x6f: return "DIVIDE";
            case 0x70: return "F1";
            case 0x71: return "F2";
            case 0x72: return "F3";
            case 0x73: return "F4";
            case 0x74: return "F5";
            case 0x75: return "F6";
            case 0x76: return "F7";
            case 0x77: return "F8";
            case 0x78: return "F9";
            case 0x79: return "F10";
            case 0x7a: return "F11";
            case 0x7b: return "F12";
            case 0x7c: return "F13";
            case 0x7d: return "F14";
            case 0x7e: return "F15";
            case 0x7f: return "F16";
            case 0x80: return "F17";
            case 0x81: return "F18";
            case 0x82: return "F19";
            case 0x83: return "F20";
            case 0x84: return "F21";
            case 0x85: return "F22";
            case 0x86: return "F23";
            case 0x87: return "F24";
            case 0x90: return "NUMLOCK";
            case 0x91: return "SCROLL";
            case 0xa0: return "LSHIFT";
            case 0xa1: return "RSHIFT";
            case 0xa2: return "LCONTROL";
            case 0xa3: return "RCONTROL";
            case 0xa4: return "LMENU";
            case 0xa5: return "RMENU";
            case 0xa6: return "BROWSER_BACK";
            case 0xa7: return "BROWSER_FORWARD";
            case 0xa8: return "BROWSER_REFRESH";
            case 0xa9: return "BROWSER_STOP";
            case 0xaa: return "BROWSER_SEARCH";
            case 0xab: return "BROWSER_FAVORITES";
            case 0xac: return "BROWSER_HOME";
            case 0xad: return "VOLUME_MUTE";
            case 0xae: return "VOLUME_DOWN";
            case 0xaf: return "VOLUME_UP";
            case 0xb0: return "MEDIA_NEXT_TRACK";
            case 0xb1: return "MEDIA_PREV_TRACK";
            case 0xb2: return "MEDIA_STOP";
            case 0xb3: return "MEDIA_PLAY_PAUSE";
            case 0xb4: return "LAUNCH_MAIL";
            case 0xb5: return "LAUNCH_MEDIA_SELECT";
            case 0xb6: return "LAUNCH_APP1";
            case 0xb7: return "LAUNCH_APP2";
            case 0xba: return "OEM_1";
            case 0xbb: return "OEM_PLUS";
            case 0xbc: return "OEM_COMMA";
            case 0xbd: return "OEM_MINUS";
            case 0xbe: return "OEM_PERIOD";
            case 0xbf: return "OEM_2";
            case 0xc0: return "OEM_3";
            case 0xc3: return "GAMEPAD_A";
            case 0xc4: return "GAMEPAD_B";
            case 0xc5: return "GAMEPAD_X";
            case 0xc6: return "GAMEPAD_Y";
            case 0xc7: return "GAMEPAD_RIGHT_SHOULDER";
            case 0xc8: return "GAMEPAD_LEFT_SHOULDER";
            case 0xc9: return "GAMEPAD_LEFT_TRIGGER";
            case 0xca: return "GAMEPAD_RIGHT_TRIGGER";
            case 0xcb: return "GAMEPAD_DPAD_UP";
            case 0xcc: return "GAMEPAD_DPAD_DOWN";
            case 0xcd: return "GAMEPAD_DPAD_LEFT";
            case 0xce: return "GAMEPAD_DPAD_RIGHT";
            case 0xcf: return "GAMEPAD_MENU";
            case 0xd0: return "GAMEPAD_VIEW";
            case 0xd1: return "GAMEPAD_LEFT_THUMBSTICK_BUTTON";
            case 0xd2: return "GAMEPAD_RIGHT_THUMBSTICK_BUTTON";
            case 0xd3: return "GAMEPAD_LEFT_THUMBSTICK_UP";
            case 0xd4: return "GAMEPAD_LEFT_THUMBSTICK_DOWN";
            case 0xd5: return "GAMEPAD_LEFT_THUMBSTICK_RIGHT";
            case 0xd6: return "GAMEPAD_LEFT_THUMBSTICK_LEFT";
            case 0xd7: return "GAMEPAD_RIGHT_THUMBSTICK_UP";
            case 0xd8: return "GAMEPAD_RIGHT_THUMBSTICK_DOWN";
            case 0xd9: return "GAMEPAD_RIGHT_THUMBSTICK_RIGHT";
            case 0xda: return "GAMEPAD_RIGHT_THUMBSTICK_LEFT";
            case 0xdb: return "OEM_4";
            case 0xdc: return "OEM_5";
            case 0xdd: return "OEM_6";
            case 0xde: return "OEM_7";
            case 0xdf: return "OEM_8";
            case 0xe2: return "OEM_102";
            case 0xe5: return "PROCESSKEY";
            case 0xe7: return "PACKET";
            case 0xf6: return "ATTN";
            case 0xf7: return "CRSEL";
            case 0xf8: return "EXSEL";
            case 0xf9: return "EREOF";
            case 0xfa: return "PLAY";
            case 0xfb: return "ZOOM";
            case 0xfc: return "NONAME";
            case 0xfd: return "PA1";
            case 0xfe: return "OEM_CLEAR";

            default: return null;
        }
    }



    public static string? VKToKeyboardLabel(ushort vk)
    {
        switch (vk)
        {
            case 0x08: return "Backspace";
            case 0x09: return "Tab";
            case 0x0d: return "Enter";
            case 0x0e: return "Enter";
            case 0x10: return "Shift";
            case 0x11: return "Ctrl";
            case 0x12: return "Alt";
            case 0x13: return "Pause\nBreak";
            case 0x14: return "Caps\nLock";
            case 0x1b: return "Esc";
            case 0x20: return "Space";
            case 0x21: return "Page\nUp";
            case 0x22: return "Page\nDown";
            case 0x23: return "End";
            case 0x24: return "Home";
            case 0x25: return "Left";
            case 0x26: return "Up";
            case 0x27: return "Right";
            case 0x28: return "Down";
            case 0x2c: return "PrtSc";
            case 0x2d: return "Insert";
            case 0x2e: return "Delete";
            case 0x30: return ")\n0";
            case 0x31: return "!\n1";
            case 0x32: return "@\n2";
            case 0x33: return "#\n3";
            case 0x34: return "$\n4";
            case 0x35: return "%\n5";
            case 0x36: return "^\n6";
            case 0x37: return "&\n7";
            case 0x38: return "*\n8";
            case 0x39: return "(\n9";
            case 0x41: return "A";
            case 0x42: return "B";
            case 0x43: return "C";
            case 0x44: return "D";
            case 0x45: return "E";
            case 0x46: return "F";
            case 0x47: return "G";
            case 0x48: return "H";
            case 0x49: return "I";
            case 0x4a: return "J";
            case 0x4b: return "K";
            case 0x4c: return "L";
            case 0x4d: return "M";
            case 0x4e: return "N";
            case 0x4f: return "O";
            case 0x50: return "P";
            case 0x51: return "Q";
            case 0x52: return "R";
            case 0x53: return "S";
            case 0x54: return "T";
            case 0x55: return "U";
            case 0x56: return "V";
            case 0x57: return "W";
            case 0x58: return "X";
            case 0x59: return "Y";
            case 0x5a: return "Z";
            case 0x5b: return "Win";
            case 0xa0: return "Shift";
            case 0xa1: return "Shift";
            case 0xa2: return "Ctrl";
            case 0xa3: return "Ctrl";
            case 0xa4: return "Alt";
            case 0xa5: return "Alt";
            case 0x5d: return "Menu";
            case 0x60: return "0\nInsert";
            case 0x61: return "1\nEnd";
            case 0x62: return "2\nDown";
            case 0x63: return "3\nPgDn";
            case 0x64: return "4\nLeft";
            case 0x65: return "5";
            case 0x66: return "6\nRight";
            case 0x67: return "7\nHome";
            case 0x68: return "8\nUp";
            case 0x69: return "9\nPgUp";
            case 0x6a: return "*";
            case 0x6b: return "+";
            case 0x6d: return "-";
            case 0x6e: return ".\nDelete";
            case 0x6f: return "/";
            case 0x70: return "F1";
            case 0x71: return "F2";
            case 0x72: return "F3";
            case 0x73: return "F4";
            case 0x74: return "F5";
            case 0x75: return "F6";
            case 0x76: return "F7";
            case 0x77: return "F8";
            case 0x78: return "F9";
            case 0x79: return "F10";
            case 0x7a: return "F11";
            case 0x7b: return "F12";
            case 0x90: return "Num\nLock";
            case 0x91: return "Scroll\nLock";
            case 0xba: return ":\n;";
            case 0xbb: return "+\n=";
            case 0xbc: return "<\n,";
            case 0xbd: return "_\n-";
            case 0xBe: return ">\n.";
            case 0xbf: return "?\n/";
            case 0xc0: return "~\n`";
            case 0xdb: return "{\n[";
            case 0xdc: return "|\n\\";
            case 0xdd: return "}\n]";
            case 0xde: return "\"\n'";

            default: return null;
        }
    }
}