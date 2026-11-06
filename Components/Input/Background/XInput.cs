using System;
using System.Runtime.InteropServices;

namespace StreamAssist.Components.Input.Background;



// windows' XInput: up to four Xbox-style controllers (and pads in an Xbox / XInput mode), read by polling. It works
// whether or not the app is focused
internal static class XInput
{
    public const int MaxControllers = 4;
    private const int ErrorSuccess = 0;



    [StructLayout(LayoutKind.Sequential)]
    public struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        public uint PacketNumber;
        public Gamepad Gamepad;
    }



    // the controller in slot index (0-3), false if none is plugged in there (or XInput isn't there)
    public static bool TryGetState(int index, out Gamepad gamepad)
    {
        gamepad = default;
        try
        {
            if (XInputGetState((uint)index, out var state) != ErrorSuccess) return false;

            gamepad = state.Gamepad;
            return true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }



    [DllImport("xinput1_4.dll")]
    private static extern int XInputGetState(uint userIndex, out State state);
}
