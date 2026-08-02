using System;

namespace FPS
{
    public static class AdaptiveDirectorDiagnostics
    {
        public static event Action<string, string> EventEmitted;

        public static void Emit(string eventName, string reason)
        {
            EventEmitted?.Invoke(eventName, reason ?? string.Empty);
        }
    }
}
