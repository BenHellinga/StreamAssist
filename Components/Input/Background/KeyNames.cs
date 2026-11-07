using System;
using System.Collections.Generic;

namespace StreamAssist.Components.Input.Background;



// key codes and their names: what the user types for a key (Key Pressed's Key: "ctrl", "F5", ";"), each code's
// windows name (MEDIA_PLAY_PAUSE) and its label on a keyboard picture ("Page\nUp")
public static class KeyNames
{
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
    public static bool TryParse(string? text, out ushort vk)
    {
        vk = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var name = text.Trim();

        if (KeyNameAliases.TryGetValue(name, out vk)) return true;

        for (ushort i = 0; i <= 0xff; ++i)
        {
            if (string.Equals(WindowsName(i), name, StringComparison.OrdinalIgnoreCase))
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

                var label = KeyboardLabel(i);
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



    public static string? WindowsName(ushort vk)
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



    public static string? KeyboardLabel(ushort vk)
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
