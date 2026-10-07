using System;

namespace QuickPickGraffiti
{
    internal static class WheelMotion
    {
        internal static float SmoothAngle(float current, float target, float deltaSeconds)
        {
            float difference = (target - current) % 360;
            if (difference > 180)
            {
                difference -= 360;
            }
            else if (difference < -180)
            {
                difference += 360;
            }

            float responseTime = 0.010f;
            float smoothingFactor = 1 - (float)Math.Exp(-Math.Max(0, deltaSeconds) / responseTime);
            float smoothedAngle = current + difference * smoothingFactor;

            // Normalize the result to ensure it stays within the 0-360 range
            return ((smoothedAngle % 360f) + 360f) % 360f;
        }

        internal static float IconScale(float iconAngle, float pointerAngle)
        {
            // Calculate the circular distance
            float difference = Math.Abs(iconAngle - pointerAngle) % 360f;
            float distance = Math.Min(difference, 360f - difference);
            return 1 + 0.65f * (float)Math.Exp(-0.5f * Math.Pow(distance / 26, 2));
        }

        internal static float TargetScale(float iconAngle, float pointerAngle, bool selected)
        {
            return selected ? 1.80f : IconScale(iconAngle, pointerAngle);
        }

        internal static float FloatX(float age, int slot)
        {
            float phase = slot * 0.5f;
            return 0.005f * (float)Math.Sin(age + phase);
        }

        internal static float FloatY(float age, int slot)
        {
            float phase = slot * 0.5f;
            return 0.005f * (float)Math.Cos(age + phase);
        }
    }
}
