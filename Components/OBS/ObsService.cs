// obs-websocket-dotnet implementation goes here.
// Suggested responsibilities:
//   - Connect / disconnect to OBS WebSocket
//   - Scene switching, source visibility, stream start/stop
//   - Expose current scene / stream status as observable properties

using System;

namespace StreamAssist.Components.OBS;

public class ObsService : IDisposable
{
    public void Dispose() { }
}
