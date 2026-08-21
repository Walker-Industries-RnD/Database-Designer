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
        private static bool _loop;

        private sealed class LoopStream : WaveStream
        {
            private readonly WaveStream _source;
            private readonly Func<bool> _shouldLoop;
            public LoopStream(WaveStream source, Func<bool> shouldLoop) { _source = source; _shouldLoop = shouldLoop; }
            public override WaveFormat WaveFormat => _source.WaveFormat;
            public override long Length => _source.Length;
            public override long Position { get => _source.Position; set => _source.Position = value; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int total = 0;
                while (total < count)
                {
                    int read = _source.Read(buffer, offset + total, count - total);
                    if (read == 0)
                    {
                        if (_source.Position == 0 || !_shouldLoop()) break;
                        _source.Position = 0;
                    }
                    total += read;
                }
                return total;
            }
        }

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
            AudioBridge.SetLoop = SetLoop;
        }

        private static void SetLoop(bool loop)
        {
            lock (_lock) { _loop = loop; }
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
                    _output = new WaveOutEvent { DesiredLatency = 200, NumberOfBuffers = 3 };
                    _output.Init(new LoopStream(_reader, () => _loop));
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
                reachedEnd = !_stopRequested && !_loop && _reader != null && _reader.Position >= _reader.Length;
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
