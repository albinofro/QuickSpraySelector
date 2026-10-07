using System;

namespace QuickPickGraffiti
{
    internal static class PresentationTiming
    {
        internal static float BlurWeight(float age)
        {
            if (float.IsNaN(age) || age <= 10f) return 0f;
            if (age >= 15f) return 1f;
            if (age <= 14f) return (age - 10f) / 4.5f;
            // Match the linear ramp's value and slope, then ease out over the final second.
            float remaining = 15f - age;
            return 1f - remaining * remaining / 9f;
        }
    }
}
