namespace FPS
{
    public static class WeaponFireClock
    {
        // Keep sub-frame lateness from accumulating; a missed interval never banks extra shots.
        public static double ShotTime(double now, double deadline, float interval, bool continuousAuto)
            => continuousAuto && deadline > 0 && now - deadline < interval
                ? deadline : System.Math.Max(now, deadline);
    }
}
