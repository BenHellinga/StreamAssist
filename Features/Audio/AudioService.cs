// NAudio implementation goes here.
// Suggested responsibilities:
//   - Play / stop / pause audio files
//   - Soundboard: map keys → audio clips via KeyboardService.KeyPressed
//   - Volume control, output device selection

using System;

namespace StreamAssist.Features.Audio;

public class AudioService : IDisposable
{
    public void Dispose() { }
}
