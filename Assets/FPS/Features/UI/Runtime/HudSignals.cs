using System;

namespace FPS
{
    // Publishers announce lifecycle changes so UI can bind regardless of creation order.
    public static class HudSignals
    {
        public static event Action SourcesChanged;
        public static void NotifySourcesChanged() => SourcesChanged?.Invoke();
    }
}
