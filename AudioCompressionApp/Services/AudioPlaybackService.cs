using NAudio.Wave;

namespace AudioCompressionApp.Services;

public class AudioPlaybackService : IDisposable {
    private WaveOutEvent? _outputDevice;

    private AudioFileReader? _audioFile;

    public bool IsPlaying =>
        _outputDevice?.PlaybackState == PlaybackState.Playing;

    public void Play(string filePath) {
        Stop();

        _audioFile = new AudioFileReader(filePath);

        _outputDevice = new WaveOutEvent();

        _outputDevice.Init(_audioFile);

        _outputDevice.Play();
    }

    public void Stop() {
        _outputDevice?.Stop();

        _outputDevice?.Dispose();

        _audioFile?.Dispose();

        _outputDevice = null;

        _audioFile = null;
    }

    public void Pause() {
        _outputDevice?.Pause();
    }

    public void Resume() {
        _outputDevice?.Play(); 
    }

    public void Dispose() {
        Stop();
    }
}