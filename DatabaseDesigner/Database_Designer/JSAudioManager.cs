using OpenSilver;
using System;

namespace Database_Designer
{
    public static class JSAudioManager
    {
        private static bool _isCustomPlayerPlaying = false;
        private static string _currentCustomSong = "";
        private static Action<double> _positionCallback;
        private static Action _endedCallback;

        public static void PlayCustomSong(string songPath, Action<double> onPositionUpdate = null, Action onEnded = null)
        {
            _positionCallback = onPositionUpdate;
            _endedCallback = onEnded;

            string escapedPath = songPath.Replace("'", "\\'").Replace("\\", "\\\\");
            string base64Script = $@"
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
                    
                    audio.onplay = function() {{
                        window.customAudioIsPlaying = true;
                    }};
                    
                    audio.onpause = function() {{
                        window.customAudioIsPlaying = false;
                    }};
                    
                    audio.onended = function() {{
                        window.customAudioIsPlaying = false;
                        if (window.onCustomAudioEnded) window.onCustomAudioEnded();
                    }};
                    
                    audio.play().catch(function(e) {{
                        console.error('Failed to play custom audio:', e);
                    }});
                    
                    window.customAudioPositionInterval = setInterval(function() {{
                        if (window.customAudioPlayer && window.customAudioPlayer.duration) {{
                            if (window.onCustomAudioPosition) {{
                                window.onCustomAudioPosition(window.customAudioPlayer.currentTime, window.customAudioPlayer.duration);
                            }}
                        }}
                    }}, 100);
                }})();
            ";

            OpenSilver.Interop.ExecuteJavaScript(base64Script);
            _isCustomPlayerPlaying = true;
            _currentCustomSong = songPath;
        }

        public static void PauseCustomSong()
        {
            OpenSilver.Interop.ExecuteJavaScript(@"
                if (window.customAudioPlayer) {
                    window.customAudioPlayer.pause();
                }
            ");
            _isCustomPlayerPlaying = false;
        }

        public static void ResumeCustomSong()
        {
            OpenSilver.Interop.ExecuteJavaScript(@"
                if (window.customAudioPlayer) {
                    window.customAudioPlayer.play().catch(function(e) { console.error(e); });
                }
            ");
            _isCustomPlayerPlaying = true;
        }

        public static void StopCustomSong()
        {
            OpenSilver.Interop.ExecuteJavaScript(@"
                if (window.customAudioPlayer) {
                    window.customAudioPlayer.pause();
                    window.customAudioPlayer.src = '';
                }
                if (window.customAudioPositionInterval) {
                    clearInterval(window.customAudioPositionInterval);
                    window.customAudioPositionInterval = null;
                }
            ");
            _isCustomPlayerPlaying = false;
            _currentCustomSong = "";
        }

        public static void SeekCustomSong(double seconds)
        {
            OpenSilver.Interop.ExecuteJavaScript($"window.customAudioPlayer.currentTime = {seconds};");
        }

        public static double GetCustomSongPosition()
        {
            var result = OpenSilver.Interop.ExecuteJavaScript(@"
                return window.customAudioPlayer ? window.customAudioPlayer.currentTime : 0;
            ");
            return Convert.ToDouble(result);
        }

        public static double GetCustomSongDuration()
        {
            var result = OpenSilver.Interop.ExecuteJavaScript(@"
                return window.customAudioPlayer && window.customAudioPlayer.duration ? window.customAudioPlayer.duration : 0;
            ");
            return Convert.ToDouble(result);
        }

        public static bool IsCustomPlaying()
        {
            var result = OpenSilver.Interop.ExecuteJavaScript(@"
                return window.customAudioIsPlaying === true ? 'true' : 'false';
            ");
            var strResult = result?.ToString() ?? "false";
            return strResult == "true";
        }

        public static void SetVolume(double volume)
        {
            OpenSilver.Interop.ExecuteJavaScript($"window.customAudioPlayer.volume = {volume};");
        }

        public static string GetCurrentCustomSong()
        {
            return _currentCustomSong;
        }

        public static void SetEndedCallback(Action callback)
        {
            _endedCallback = callback;
            OpenSilver.Interop.ExecuteJavaScript($@"
                window.onCustomAudioEnded = function() {{}};
            ");
        }
    }
}