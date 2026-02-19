using System;
using System.Threading.Tasks;
using NAudio.Wave;

namespace AutoCinema.Desktop.Services;

public class NAudioPlayerService : IAudioPlayerService, IDisposable
{
    private WaveOutEvent? _outputDevice;
    private AudioFileReader? _audioFile;

    public event EventHandler? PlaybackStopped;

    public async Task PlayAsync(string audioFilePath)
    {
        await StopAsync();

        try
        {
            _audioFile = new AudioFileReader(audioFilePath);
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_audioFile);
            _outputDevice.PlaybackStopped += OnPlaybackStopped;
            _outputDevice.Play();
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            await StopAsync();
            throw new Exception($"Audio playback failed: {ex.Message}", ex);
        }
    }

    public Task StopAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_outputDevice != null)
        {
            _outputDevice.Stop();
            _outputDevice.PlaybackStopped -= OnPlaybackStopped;
            _outputDevice.Dispose();
            _outputDevice = null;
        }

        if (_audioFile != null)
        {
            _audioFile.Dispose();
            _audioFile = null;
        }
    }
}


