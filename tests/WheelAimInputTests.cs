using System;
using QuickPickGraffiti;

internal static class WheelAimInputTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    internal static void Run()
    {
        var aim = new WheelAimInput(); float x, y;
        Check(aim.Update(0, 1, 0, 0, 0, 0, false, false, out x, out y) && y == 1, "Left stick starts aim");
        Check(aim.Update(1, 0, 0, 0, 0, 0, false, false, out x, out y) && x == 1, "Full-tilt left rotation");
        Check(aim.Update(1, 0, 0, -1, 0, 0, false, false, out x, out y) && y == -1, "Right takes over held left");
        Check(aim.Update(1, 0, -1, 0, 0, 0, false, false, out x, out y) && x == -1, "Full-tilt right rotation");
        Check(aim.Update(1, 0, -1, 0, 0, 0, false, false, out x, out y) && x == -1, "Held left cannot steal right");
        Check(aim.Update(0, 1, -1, 0, 0, 0, false, false, out x, out y) && y == 1, "Left deliberate handover");
        Check(aim.Update(0, 1, -1, 0, 1, 0, true, false, out x, out y) && x == 1 && aim.MouseActive, "Mouse takes over held sticks");
        Check(!aim.Update(0, 1, -1, 0, 1, 0, false, false, out x, out y) && aim.MouseActive, "Held sticks cannot steal mouse");
        Check(aim.Update(0, 1, 0, -1, 1, 0, false, false, out x, out y) && y == -1 && !aim.MouseActive, "Right takes over stationary mouse");
        Check(!aim.Update(0, 1, 0, -1, 0, 0, true, false, out x, out y) && aim.MouseActive, "Center mouse preserves selection");
        Check(!aim.Update(0, 1, 0, -1, 1, 0, false, true, out x, out y), "Discrete step has priority");
        Check(!aim.Update(0, 1, 0, -1, 1, 0, false, true, out x, out y), "Consecutive discrete steps");
        Check(!aim.Update(0, 1, 0, -1, 1, 0, false, false, out x, out y), "Held inputs stay suppressed after step");
        Check(aim.Update(1, 0, 0, -1, 1, 0, false, false, out x, out y) && x == 1, "Intentional rotation resumes aim");
        Check(aim.Update(0, 0, 0, -1, 1, 0, false, false, out x, out y) && y == -1, "Release falls back to other held stick");
        Check(!aim.Update(0.1f, 0.1f, 0, 0, 1, 0, false, false, out x, out y), "Deadzone releases owner");
        aim.Reset();
        Check(!aim.MouseActive && !aim.Update(0, 0, 0, 0, 1, 0, false, false, out x, out y), "Reset clears ownership");
        Check(aim.Update(0, 0, 0, 1, 0, 0, false, false, out x, out y) && y == 1, "Right stick works alone");
        Check(aim.Update(0, 0, 0, 1, -1, 0, true, false, out x, out y) && x == -1, "Mouse handover");
        for (int i = 1; i < 5; i++)
            Check(!aim.Update(0, 0, 0.01f * i, 1, -1, 0, false, false, out x, out y), "Tiny stick jitter cannot steal mouse");
        Check(aim.Update(0, 0, 0.06f, 1, -1, 0, false, false, out x, out y) && !aim.MouseActive, "Slow deliberate stick change accumulates");
        var wheel = new WheelSelection(24);
        wheel.UpdateStick(0, 1, false); wheel.ChangePage(1);
        Check(!aim.Update(0, 0, 0.06f, 1, -1, 0, false, true, out x, out y), "Page change suppresses resting aim");
        Check(!aim.Update(0, 0, 0.06f, 1, -1, 0, false, false, out x, out y), "No page change snap back");
        Check(wheel.SelectedIndex == 12, "Page choice retained");
        Console.WriteLine("Both-stick rotation, mouse handover, deadzones and discrete-input suppression checks passed.");
    }
}