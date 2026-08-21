using OpenSilver;
using System;

namespace Database_Designer
{
    public static class JSAudioManager
    {
        private static bool _isCustomPlayerPlaying = false;
        private static string _currentCustomSong = "";

        public static void PlayCustomSong(string songPath, Action<double> onPositionUpdate = null, Action onEnded = null)
        {
            _currentCustomSong = songPath;
            _isCustomPlayerPlaying = true;

            if (AudioBridge.Available)
            {
                AudioBridge.Play(songPath);
                return;
            }

            string escapedPath = songPath.Replace("'", "\\'").Replace("\\", "\\\\");
            string script = $@"
                (function() {{
                    if (window.customAudioPlayer) {{
                        window.customAudioPlayer.pause();
                        window.customAudioPlayer.src = '';
                    }}
                    var audio = new Audio();
                    audio.src = '{escapedPath}';
                    audio.loop = false;
                    audio.volume = 0.7;
                    window.customAudioPlayer = audio;
                    audio.onended = function() {{
                        window.customAudioIsPlaying = false;
                        if (window.onCustomAudioEnded) window.onCustomAudioEnded();
                    }};
                    audio.play().catch(function(e) {{ console.error('Failed to play custom audio:', e); }});
                }})();
            ";
            OpenSilver.Interop.ExecuteJavaScript(script);
        }

        public static void PauseCustomSong()
        {
            _isCustomPlayerPlaying = false;
            if (AudioBridge.Available) { AudioBridge.Pause?.Invoke(); return; }
            OpenSilver.Interop.ExecuteJavaScript("if (window.customAudioPlayer) { window.customAudioPlayer.pause(); }");
        }

        public static void ResumeCustomSong()
        {
            _isCustomPlayerPlaying = true;
            if (AudioBridge.Available) { AudioBridge.Resume?.Invoke(); return; }
            OpenSilver.Interop.ExecuteJavaScript("if (window.customAudioPlayer) { window.customAudioPlayer.play().catch(function(e) { console.error(e); }); }");
        }

        public static void StopCustomSong()
        {
            _isCustomPlayerPlaying = false;
            _currentCustomSong = "";
            if (AudioBridge.Available) { AudioBridge.Stop?.Invoke(); return; }
            OpenSilver.Interop.ExecuteJavaScript(@"
                if (window.customAudioPlayer) { window.customAudioPlayer.pause(); window.customAudioPlayer.src = ''; }
            ");
        }

        public static void SeekCustomSong(double seconds)
        {
            if (AudioBridge.Available) { AudioBridge.Seek?.Invoke(seconds); return; }
            OpenSilver.Interop.ExecuteJavaScript($"if (window.customAudioPlayer) window.customAudioPlayer.currentTime = {seconds};");
        }

        public static double GetCustomSongPosition()
        {
            if (AudioBridge.Available) return AudioBridge.Position?.Invoke() ?? 0;
            var result = OpenSilver.Interop.ExecuteJavaScript("return window.customAudioPlayer ? window.customAudioPlayer.currentTime : 0;");
            return Convert.ToDouble(result);
        }

        public static double GetCustomSongDuration()
        {
            if (AudioBridge.Available) return AudioBridge.Duration?.Invoke() ?? 0;
            var result = OpenSilver.Interop.ExecuteJavaScript("return window.customAudioPlayer && window.customAudioPlayer.duration ? window.customAudioPlayer.duration : 0;");
            return Convert.ToDouble(result);
        }

        public static bool IsCustomPlaying()
        {
            if (AudioBridge.Available) return AudioBridge.Playing?.Invoke() ?? false;
            var result = OpenSilver.Interop.ExecuteJavaScript("return window.customAudioIsPlaying === true ? 'true' : 'false';");
            return (result?.ToString() ?? "false") == "true";
        }

        public static void SetVolume(double volume)
        {
            if (AudioBridge.Available) { AudioBridge.Volume?.Invoke(volume); return; }
            OpenSilver.Interop.ExecuteJavaScript($"if (window.customAudioPlayer) window.customAudioPlayer.volume = {volume};");
        }

        public static void SetLoop(bool loop)
        {
            if (AudioBridge.Available) { AudioBridge.SetLoop?.Invoke(loop); return; }
            OpenSilver.Interop.ExecuteJavaScript($"if (window.customAudioPlayer) window.customAudioPlayer.loop = {(loop ? "true" : "false")};");
        }

        public static string GetCurrentCustomSong() => _currentCustomSong;

        public static bool IsCustomPlayerActive() => _isCustomPlayerPlaying;
    }
}
