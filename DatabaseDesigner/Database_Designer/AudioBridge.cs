using System;

namespace Database_Designer
{
    public static class AudioBridge
    {
        public static Func<string, bool> Play;
        public static Action Pause;
        public static Action Resume;
        public static Action Stop;
        public static Action<double> Seek;
        public static Action<double> Volume;
        public static Func<double> Position;
        public static Func<double> Duration;
        public static Func<bool> Playing;
        public static Action<bool> SetLoop;
        public static Action Ended;

        public static bool Available => Play != null;

        public static void RaiseEnded() => Ended?.Invoke();
    }
}
