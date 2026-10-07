using System;

namespace QuickPickGraffiti
{
    // Allocation‑free target follower for a medium‑strength orbital camera attachment.
    // Current position and velocity are kept separately from the target.
    internal sealed class CameraMotionState
    {
        // Current state
        private double x, y, vx, vy;
        // Target state
        private double targetX, targetY;

        // Public read‑only accessors
        internal float X { get { return (float)x; } }
        internal float Y { get { return (float)y; } }

        // Reset clears current and target state.
        internal void Reset()
        {
            x = y = vx = vy = 0.0;
            targetX = targetY = 0.0;
        }

        // Set the desired target position based on screen coordinates.
        // screenX in [-1,1] maps to targetX in [-1,1] with a sign flip.
        // screenY in [-1,1] maps directly to targetY.
        internal void SetTarget(float screenX, float screenY)
        {
            if (float.IsNaN(screenX) || float.IsInfinity(screenX) ||
                float.IsNaN(screenY) || float.IsInfinity(screenY))
                return;

            // Manual clamp to [-1,1]
            double clampedX = screenX;
            if (clampedX < -1.0) clampedX = -1.0;
            else if (clampedX > 1.0) clampedX = 1.0;

            double clampedY = screenY;
            if (clampedY < -1.0) clampedY = -1.0;
            else if (clampedY > 1.0) clampedY = 1.0;

            targetX = -clampedX; // left (-1) => positive X, right (+1) => negative X
            targetY = clampedY;  // Y maps directly
        }

        // Advance the state by dt seconds using an exact critically damped solution.
        internal void Step(float dt)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
                return;

            const double omega = 12.0; // critical damping frequency

            // X axis
            double offsetX = x - targetX;
            double tempX = (vx + omega * offsetX) * dt;
            double decayX = Math.Exp(-omega * dt);
            x = targetX + (offsetX + tempX) * decayX;
            vx = (vx - omega * tempX) * decayX;

            // Y axis
            double offsetY = y - targetY;
            double tempY = (vy + omega * offsetY) * dt;
            double decayY = Math.Exp(-omega * dt);
            y = targetY + (offsetY + tempY) * decayY;
            vy = (vy - omega * tempY) * decayY;

            // Idle shortcut: if close enough to target, snap exactly.
            if (Math.Abs(offsetX) < 1e-6 && Math.Abs(vx) < 1e-6)
            {
                x = targetX;
                vx = 0.0;
            }
            if (Math.Abs(offsetY) < 1e-6 && Math.Abs(vy) < 1e-6)
            {
                y = targetY;
                vy = 0.0;
            }
        }
    }
}
