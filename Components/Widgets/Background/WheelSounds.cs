using System;
using System.IO;
using NAudio.Wave;

namespace StreamAssist.Components.Widgets.Background;



// the wheel's built-in sounds, made here rather than shipped as files: a short wooden tick as a slice passes the pointer,
// and a bright two-note chime for the winner. Each call gives a fresh stream (the player disposes it)
public static class WheelSounds
{
    // constants



    private const int Rate = 44100;



    // private



    private static readonly byte[] TickSamples = Make(0.03, t =>
        (Math.Sin(2 * Math.PI * 1900 * t) * 0.6 + Math.Sin(2 * Math.PI * 3700 * t) * 0.25) * Math.Exp(-t / 0.006));

    // C6 then G6, each a soft bell (a fundamental and a quieter octave, fading out)
    private static readonly byte[] ChimeSamples = Make(1.1, t =>
        Bell(t, 1046.5) * 0.5 + (t > 0.12 ? Bell(t - 0.12, 1568.0) * 0.5 : 0));



    // public api



    public static WaveStream Tick() => Stream(TickSamples);
    public static WaveStream Chime() => Stream(ChimeSamples);



    // helpers



    private static double Bell(double t, double frequency) =>
        (Math.Sin(2 * Math.PI * frequency * t) + 0.3 * Math.Sin(2 * Math.PI * frequency * 2 * t)) * Math.Exp(-t / 0.35) * Math.Min(1, t / 0.005);



    // 16 bit mono samples of the shape over that many seconds
    private static byte[] Make(double seconds, Func<double, double> shape)
    {
        var count = (int)(seconds * Rate);
        var bytes = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var sample = (short)(Math.Clamp(shape((double)i / Rate), -1, 1) * short.MaxValue * 0.8);
            bytes[i * 2] = (byte)sample;
            bytes[i * 2 + 1] = (byte)(sample >> 8);
        }
        return bytes;
    }



    private static WaveStream Stream(byte[] samples) => new RawSourceWaveStream(new MemoryStream(samples), new WaveFormat(Rate, 16, 1));
}
