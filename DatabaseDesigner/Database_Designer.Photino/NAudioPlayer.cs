using System;
using Database_Designer;
using NAudio.Wave;

namespace Database_Designer.Photino
{
    public static class NAudioPlayer
    {
        private static readonly object _lock = new();
        private static WaveOutEvent _output;
        private static AudioFileReader _reader;
        private static float _volume = 0.7f;
        private static bool _stopRequested;

        public static void Register()
        {
            AudioBridge.Play = Play;
            AudioBridge.Pause = Pause;
            AudioBridge.Resume = Resume;
            AudioBridge.Stop = Stop;
            AudioBridge.Seek = Seek;
            AudioBridge.Volume = SetVolume;
            AudioBridge.Position = Position;
            AudioBridge.Duration = Duration;
            AudioBridge.Playing = Playing;
        }

        private static bool Play(string path)
        {
            lock (_lock)
            {
                try
                {
                    StopInternal();

                    if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                        path = new Uri(path).LocalPath;

                    _reader = new AudioFileReader(path) { Volume = _volume };
                    _output = new WaveOutEvent();
                    _output.Init(_reader);
                    _output.PlaybackStopped += OnPlaybackStopped;
                    _stopRequested = false;
                    _output.Play();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NAudio] Play failed: {ex.Message}");
                    return false;
                }
            }
        }

        private static void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            bool reachedEnd;
            lock (_lock)
            {
                reachedEnd = !_stopRequested && _reader != null && _reader.Position >= _reader.Length;
            }
            if (reachedEnd) AudioBridge.RaiseEnded();
        }

        private static void Pause()
        {
            lock (_lock) { try { _output?.Pause(); } catch { } }
        }

        private static void Resume()
        {
            lock (_lock) { try { _output?.Play(); } catch { } }
        }

        private static void Stop()
        {
            lock (_lock) { StopInternal(); }
        }

        private static void StopInternal()
        {
            _stopRequested = true;
            try
            {
                if (_output != null)
                {
                    _output.PlaybackStopped -= OnPlaybackStopped;
                    _output.Stop();
                    _output.Dispose();
                    _output = null;
                }
                _reader?.Dispose();
                _reader = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NAudio] Stop failed: {ex.Message}");
            }
        }

        private static void Seek(double seconds)
        {
            lock (_lock)
            {
                try { if (_reader != null) _reader.CurrentTime = TimeSpan.FromSeconds(seconds); }
                catch { }
            }
        }

        private static void SetVolume(double volume)
        {
            lock (_lock)
            {
                _volume = (float)Math.Max(0, Math.Min(1, volume));
                try { if (_reader != null) _reader.Volume = _volume; } catch { }
            }
        }

        private static double Position()
        {
            lock (_lock) { try { return _reader?.CurrentTime.TotalSeconds ?? 0; } catch { return 0; } }
        }

        private static double Duration()
        {
            lock (_lock) { try { return _reader?.TotalTime.TotalSeconds ?? 0; } catch { return 0; } }
        }

        private static bool Playing()
        {
            lock (_lock) { return _output != null && _output.PlaybackState == PlaybackState.Playing; }
        }
    }
}
