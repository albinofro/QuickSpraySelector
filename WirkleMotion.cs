using System;

namespace QuickPickGraffiti
{
    internal static class WirkleMotion
    {
        internal const float RevealSeconds = 0.2f;
        internal static float Reveal(float age)
        {
            float t = Math.Max(0f, Math.Min(1f, age / RevealSeconds));
            return t * t * (3f - 2f * t);
        }
        internal static float Outer(float angle)
        {
            return .945f + .035f * (float)Math.Cos(8f * angle) + .013f * (float)Math.Sin(13f * angle);
        }
        private static float Smooth(float t)
        {
            t=Math.Max(0f,Math.Min(1f,t));
            return t*t*(3f-2f*t);
        }
        internal static float RevealBand(float reveal, float across)
        {
            if (reveal <= 0f) return 0f;
            if (reveal >= 1f) return 1f;
            // Mask from the outside edge inward; never scale geometry or texture UVs.
            return Smooth((reveal - (1f-across)*.94f) / .06f);
        }
        internal static float SprayFlight(float cycleAge)
        {
            return Math.Max(0f, Math.Min(1f, cycleAge / .04f));
        }
        internal static float SprayFade(float age, float delay, float lifetime)
        {
            if (age<delay || lifetime<=0f) return 0f;
            float cycleAge=(age-delay)%lifetime;
            return Math.Max(0f,Math.Min(1f,(lifetime-cycleAge)/.4f));
        }
        internal struct FlowTime
        {
            internal float SinA, CosA, SinB, CosB, SinT, CosT;
            internal FlowTime(float age)
            {
                double t = age * .14;
                SinT = (float)Math.Sin(t); CosT = (float)Math.Cos(t);
                SinA = (float)Math.Sin(.48 * SinT); CosA = (float)Math.Cos(.48 * SinT);
                SinB = (float)Math.Sin(.56 * CosT); CosB = (float)Math.Cos(.56 * CosT);
            }
        }
        internal struct FlowPoint
        {
            private float basis, sinA, cosA, sinB, cosB, sinC, cosC;
            internal FlowPoint(float angle, float radius)
            {
                // Stretch the material field vertically, without deforming the wirkle itself.
                float x = (float)Math.Sin(angle) * radius, y = (float)Math.Cos(angle) * radius * .55f;
                // Fold an initially layered field into stretched pools and fine veins.
                // Spatial warps are cached; per-frame work uses three shared phases.
                float p=x+.35f*(float)Math.Sin(3f*y)+.2f*(float)Math.Cos(5f*x);
                float q=y+.35f*(float)Math.Sin(4f*x)-.15f*(float)Math.Cos(5f*y);
                basis = x*1.3f+y*.5f;
                sinA=(float)Math.Sin(p*4.8f+q*.9f); cosA=(float)Math.Cos(p*4.8f+q*.9f);
                sinB=(float)Math.Sin(q*7.5f-p*1.6f); cosB=(float)Math.Cos(q*7.5f-p*1.6f);
                sinC=(float)Math.Sin(p*15f+q*5f); cosC=(float)Math.Cos(p*15f+q*5f);
            }
            internal float Sample(FlowTime time)
            {
                return basis + 1.2f*(sinA*time.CosA + cosA*time.SinA)
                    + .55f*(sinB*time.CosB + cosB*time.SinB)
                    + .15f*(sinC*time.CosT + cosC*time.SinT);
            }
        }
    }
}
