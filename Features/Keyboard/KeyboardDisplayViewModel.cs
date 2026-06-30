using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using StreamAssist.Features.Shell;

namespace StreamAssist.Features.Keyboard;



/// <summary>
/// Drives the KeyboardDisplay view (the rendered keyboard canvas).
/// </summary>
public partial class KeyboardDisplayViewModel : ViewModelBase
{
    // general object classes
    private readonly InputManager _inputManager;

    // keyboard display construction
    private const double _KEY_SIZE = 55;
    private const double _KEY_MARGIN = 7;
    private const bool _INCLUDE_UNUSEABLE = true;
    public double CanvasWidth { get; init; } = _KEY_SIZE * 22 + _KEY_MARGIN * 24;
    public double CanvasHeight { get; init; } = _KEY_SIZE * 6 + _KEY_MARGIN * 6;
    public ObservableCollection<KeyInfo> Keys { get; } = [];
    private readonly Dictionary<ushort, KeyInfo> _keyMap = [];



    // constructor



    public KeyboardDisplayViewModel(InputManager inputManager)
    {
        _inputManager = inputManager;

        inputManager.KeyPressed  += vk => Dispatcher.UIThread.Post(() => SetKeyState(vk, true));
        inputManager.KeyReleased += vk => Dispatcher.UIThread.Post(() => SetKeyState(vk, false));

        InitKeys();
    }



    // helper methods



    private void InitKeys()
    {
        const double _KEY_FULL = _KEY_SIZE + _KEY_MARGIN;
        const double _ARROW_SECTION = _KEY_FULL * 15 + _KEY_MARGIN;
        const double _NUMPAD_SECTION = _ARROW_SECTION + _KEY_FULL * 3 + _KEY_MARGIN;

        var keys = new List<(string Label, ushort VK, double X, double Y, double W, double H, bool useable)>
        {
            // function row
            ("Esc",             0x1B, 0, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F1",              0x70, _KEY_FULL * 2, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F2",              0x71, _KEY_FULL * 3, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F3",              0x72, _KEY_FULL * 4, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F4",              0x73, _KEY_FULL * 5, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F5",              0x74, _KEY_FULL * 7 - _KEY_SIZE * 0.5, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F6",              0x75, _KEY_FULL * 8 - _KEY_SIZE * 0.5, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F7",              0x76, _KEY_FULL * 9 - _KEY_SIZE * 0.5, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F8",              0x77, _KEY_FULL * 10 - _KEY_SIZE * 0.5, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F9",              0x78, _KEY_FULL * 11, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F10",             0x79, _KEY_FULL * 12, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F11",             0x7A, _KEY_FULL * 13, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("F12",             0x7B, _KEY_FULL * 14, 0, _KEY_SIZE, _KEY_SIZE, true),
            ("PrtSc",           0x2C, _ARROW_SECTION, 0, _KEY_SIZE, _KEY_SIZE, false),
            ("Scroll\n Lock",   0x91, _ARROW_SECTION + _KEY_FULL, 0, _KEY_SIZE, _KEY_SIZE, false),
            ("Pause\nBreak",    0x13, _ARROW_SECTION + _KEY_FULL * 2, 0, _KEY_SIZE, _KEY_SIZE, false),

            // number row
            ("~\n`",            0xC0, 0, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("!\n1",            0x31, _KEY_FULL, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("@\n2",            0x32, _KEY_FULL * 2, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("#\n3",            0x33, _KEY_FULL * 3, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("$\n4",            0x34, _KEY_FULL * 4, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("%\n5",            0x35, _KEY_FULL * 5, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("^\n6",            0x36, _KEY_FULL * 6, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("&\n7",            0x37, _KEY_FULL * 7, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("*\n8",            0x38, _KEY_FULL * 8, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("(\n9",            0x39, _KEY_FULL * 9, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            (")\n0",            0x30, _KEY_FULL * 10, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("_\n-",            0xBD, _KEY_FULL * 11, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("+\n=",            0xBB, _KEY_FULL * 12, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("Backspace",       0x08, _KEY_FULL * 13, _KEY_FULL, _KEY_SIZE * 2 + _KEY_MARGIN, _KEY_SIZE, true),
            ("Insert",          0x2D, _ARROW_SECTION, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("Home",            0x24, _ARROW_SECTION + _KEY_FULL, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("Page\n Up",       0x21, _ARROW_SECTION + _KEY_FULL * 2, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("Num\nLock",       0x90, _NUMPAD_SECTION, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("/",               0x6F, _NUMPAD_SECTION + _KEY_FULL, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("*",               0x6A, _NUMPAD_SECTION + _KEY_FULL * 2, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),
            ("-",               0x6D, _NUMPAD_SECTION + _KEY_FULL * 3, _KEY_FULL, _KEY_SIZE, _KEY_SIZE, true),

            // QWERTY row
            ("Tab",             0x09, 0, _KEY_FULL * 2, _KEY_SIZE * 1.5, _KEY_SIZE, true),
            ("Q",               0x51, _KEY_SIZE * 1.5 + _KEY_MARGIN, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("W",               0x57, _KEY_SIZE * 0.5 + _KEY_FULL * 2, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("E",               0x45, _KEY_SIZE * 0.5 + _KEY_FULL * 3, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("R",               0x52, _KEY_SIZE * 0.5 + _KEY_FULL * 4, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("T",               0x54, _KEY_SIZE * 0.5 + _KEY_FULL * 5, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("Y",               0x59, _KEY_SIZE * 0.5 + _KEY_FULL * 6, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("U",               0x55, _KEY_SIZE * 0.5 + _KEY_FULL * 7, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("I",               0x49, _KEY_SIZE * 0.5 + _KEY_FULL * 8, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("O",               0x4F, _KEY_SIZE * 0.5 + _KEY_FULL * 9, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("P",               0x50, _KEY_SIZE * 0.5 + _KEY_FULL * 10, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("{\n[",            0xDB, _KEY_SIZE * 0.5 + _KEY_FULL * 11, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("}\n]",            0xDD, _KEY_SIZE * 0.5 + _KEY_FULL * 12, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("|\n\\",           0xDC, _KEY_SIZE * 0.5 + _KEY_FULL * 13, _KEY_FULL * 2, _KEY_SIZE * 1.5 + _KEY_MARGIN, _KEY_SIZE, true),
            ("Delete",          0x2E, _ARROW_SECTION, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("End",             0x23, _ARROW_SECTION + _KEY_FULL, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("Page\nDown",      0x22, _ARROW_SECTION + _KEY_FULL * 2, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            (" 7\nHome",        0x67, _NUMPAD_SECTION, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("8\nUp",           0x68, _NUMPAD_SECTION + _KEY_FULL, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            (" 9\nPgUp",        0x69, _NUMPAD_SECTION + _KEY_FULL * 2, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE, true),
            ("+",               0x6B, _NUMPAD_SECTION + _KEY_FULL * 3, _KEY_FULL * 2, _KEY_SIZE, _KEY_SIZE * 2 + _KEY_MARGIN, true),

            // ASDF row
            ("Caps\nLock",      0x14, 0, _KEY_FULL * 3, _KEY_SIZE * 1.75, _KEY_SIZE, true),
            ("A",               0x41, _KEY_SIZE * 1.75 + _KEY_MARGIN, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("S",               0x53, _KEY_SIZE * 0.75 + _KEY_FULL * 2, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("D",               0x44, _KEY_SIZE * 0.75 + _KEY_FULL * 3, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("F",               0x46, _KEY_SIZE * 0.75 + _KEY_FULL * 4, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("G",               0x47, _KEY_SIZE * 0.75 + _KEY_FULL * 5, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("H",               0x48, _KEY_SIZE * 0.75 + _KEY_FULL * 6, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("J",               0x4A, _KEY_SIZE * 0.75 + _KEY_FULL * 7, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("K",               0x4B, _KEY_SIZE * 0.75 + _KEY_FULL * 8, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("L",               0x4C, _KEY_SIZE * 0.75 + _KEY_FULL * 9, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            (":\n;",            0xBA, _KEY_SIZE * 0.75 + _KEY_FULL * 10, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("\"\n'",           0xDE, _KEY_SIZE * 0.75 + _KEY_FULL * 11, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("Enter",           0x0D, _KEY_SIZE * 0.75 + _KEY_FULL * 12, _KEY_FULL * 3, _KEY_SIZE * 2.25 + _KEY_MARGIN * 2, _KEY_SIZE, true),
            (" 4\nLeft",        0x64, _NUMPAD_SECTION, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("5",               0x65, _NUMPAD_SECTION + _KEY_FULL, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),
            ("  6\nRight",      0x66, _NUMPAD_SECTION + _KEY_FULL * 2, _KEY_FULL * 3, _KEY_SIZE, _KEY_SIZE, true),

            // ZXCV row
            ("Shift",           0xA0, 0, _KEY_FULL * 4, _KEY_SIZE * 2.1 + _KEY_MARGIN, _KEY_SIZE, false),
            ("Z",               0x5A, _KEY_SIZE * 0.1 + _KEY_FULL * 2, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("X",               0x58, _KEY_SIZE * 0.1 + _KEY_FULL * 3, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("C",               0x43, _KEY_SIZE * 0.1 + _KEY_FULL * 4, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("V",               0x56, _KEY_SIZE * 0.1 + _KEY_FULL * 5, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("B",               0x42, _KEY_SIZE * 0.1 + _KEY_FULL * 6, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("N",               0x4E, _KEY_SIZE * 0.1 + _KEY_FULL * 7, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("M",               0x4D, _KEY_SIZE * 0.1 + _KEY_FULL * 8, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("<\n,",            0xBC, _KEY_SIZE * 0.1 + _KEY_FULL * 9, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            (">\n.",            0xBE, _KEY_SIZE * 0.1 + _KEY_FULL * 10, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("?\n/",            0xBF, _KEY_SIZE * 0.1 + _KEY_FULL * 11, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("Shift",           0xA1, _KEY_SIZE * 0.1 + _KEY_FULL * 12, _KEY_FULL * 4, _KEY_SIZE * 2.9 + _KEY_MARGIN * 2, _KEY_SIZE, false),
            ("Up",              0x26, _ARROW_SECTION + _KEY_FULL, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            (" 1\nEnd",         0x61, _NUMPAD_SECTION, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            (" 2\nDown",        0x62, _NUMPAD_SECTION + _KEY_FULL, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            (" 3\nPgDn",        0x63, _NUMPAD_SECTION + _KEY_FULL * 2, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE, true),
            ("Enter",           0,    _NUMPAD_SECTION + _KEY_FULL * 3, _KEY_FULL * 4, _KEY_SIZE, _KEY_SIZE * 2 + _KEY_MARGIN, false),

            // bottom row
            ("Ctrl",            0xA2, 0, _KEY_FULL * 5, _KEY_SIZE * 1.1, _KEY_SIZE, false),
            ("Fn",              0,    _KEY_SIZE * 1.1 + _KEY_MARGIN, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, false),
            ("Win",             0x5B, _KEY_SIZE * 0.1 + _KEY_FULL * 2, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
            ("Alt",             0xA4, _KEY_SIZE * 0.1 + _KEY_FULL * 3, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, false),
            ("Space",           0x20, _KEY_SIZE * 0.1 + _KEY_FULL * 4, _KEY_FULL * 5, _KEY_SIZE * 6 + _KEY_MARGIN * 5, _KEY_SIZE, true),
            ("Alt",             0xA5, _KEY_SIZE * 0.1 + _KEY_FULL * 10, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, false),
            ("Fn",              0,    _KEY_SIZE * 0.1 + _KEY_FULL * 11, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, false),
            ("Menu",            0x5D, _KEY_SIZE * 0.1 + _KEY_FULL * 12, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
            ("Ctrl",            0xA3, _KEY_SIZE * 0.1 + _KEY_FULL * 13, _KEY_FULL * 5, _KEY_SIZE * 1.9 + _KEY_MARGIN, _KEY_SIZE, false),
            ("Left",            0x25, _ARROW_SECTION, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
            ("Down",            0x28, _ARROW_SECTION + _KEY_FULL, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
            ("Right",           0x27, _ARROW_SECTION + _KEY_FULL * 2, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
            ("  0\nInsert",     0x60, _NUMPAD_SECTION, _KEY_FULL * 5, _KEY_SIZE * 2 + _KEY_MARGIN, _KEY_SIZE, true),
            ("  .\nDelete",     0x6E, _NUMPAD_SECTION + _KEY_FULL * 2, _KEY_FULL * 5, _KEY_SIZE, _KEY_SIZE, true),
        };

        foreach (var (label, vk, x, y, w, h, useable) in keys)
        {
            if (!(_INCLUDE_UNUSEABLE || useable)) continue;

            var key = new KeyInfo(label, vk, x, y, w, h);
            Keys.Add(key);
            _keyMap.TryAdd(vk, key);
        }
    }



    private void SetKeyState(ushort vk, bool isDown)
    {
        if (_keyMap.TryGetValue(vk, out var key))
            key.IsPressed = isDown;
    }
}
