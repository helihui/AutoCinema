using System;
using System.Threading.Tasks;

namespace AutoCinema.Desktop.Services;

public interface IAudioPlayerService
{
    Task PlayAsync(string audioFilePath);
    Task StopAsync();
    event EventHandler PlaybackStopped;
}


