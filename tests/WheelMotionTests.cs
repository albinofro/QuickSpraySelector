using System;
using QuickPickGraffiti;

internal static class WheelMotionTests
{
    private static int assertions;

    private static void Equal(float expected, float actual, string name, float tolerance = 0.001f)
    {
        assertions++;
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"Assertion failed: {name} - Expected: {expected}, Actual: {actual}");
    }

    private static void Equal(int expected, int actual, string name)
    {
        assertions++;
        if (expected != actual)
            throw new Exception($"Assertion failed: {name} - Expected: {expected}, Actual: {actual}");
    }

    internal static void Run()
    {
        // Test SmoothAngle
        float current = 0;
        float target = 180;
        float deltaSeconds = 0.1f;
        float result = WheelMotion.SmoothAngle(current, target, deltaSeconds);
        float expectedSmooth = 180f * (1f - (float)Math.Exp(-10f));
        Equal(expectedSmooth, result, "SmoothAngle");

        // Test IconScale
        float iconAngle = 0;
        float pointerAngle = 0;
        float expected = 1.65f;
        float actual = WheelMotion.IconScale(iconAngle, pointerAngle);
        Equal(expected, actual, "IconScale");

        // Test TargetScale
        bool selected = true;
        float expectedTarget = 1.80f;
        float actualTarget = WheelMotion.TargetScale(iconAngle, pointerAngle, selected);
        Equal(expectedTarget, actualTarget, "TargetScale");

        // Test FloatX
        float age = 0;
        int slot = 0;
        float expectedFloatX = 0;
        float actualFloatX = WheelMotion.FloatX(age, slot);
        Equal(expectedFloatX, actualFloatX, "FloatX");

        // Test FloatY
        float expectedFloatY = 0.005f;
        float actualFloatY = WheelMotion.FloatY(age, slot);
        Equal(expectedFloatY, actualFloatY, "FloatY");

        // Test IconScale symmetry
        iconAngle = 1;
        pointerAngle = 359;
        float result1 = WheelMotion.IconScale(iconAngle, pointerAngle);
        iconAngle = 359;
        pointerAngle = 1;
        float result2 = WheelMotion.IconScale(iconAngle, pointerAngle);
        Equal(result1, result2, "IconScale symmetry");

        // Test IconScale edge case
        iconAngle = 720;
        pointerAngle = 0;
        expected = 1.65f;
        actual = WheelMotion.IconScale(iconAngle, pointerAngle);
        Equal(expected, actual, "IconScale edge case");

        // Test TargetScale comparison
        iconAngle = 180;
        pointerAngle = 0;
        selected = true;
        float targetScale1 = WheelMotion.TargetScale(iconAngle, pointerAngle, selected);
        iconAngle = 0;
        pointerAngle = 0;
        selected = false;
        float targetScale2 = WheelMotion.TargetScale(iconAngle, pointerAngle, selected);
        if (targetScale1 <= targetScale2)
            throw new Exception("TargetScale comparison failed");

        // Test SmoothAngle edge case
        current = -720;
        target = 0;
        deltaSeconds = 0;
        result = WheelMotion.SmoothAngle(current, target, deltaSeconds);
        Equal(0, result, "SmoothAngle edge case");

        // Test SmoothAngle response time
        current = 0;
        target = 90;
        deltaSeconds = 0.01f;
        result = WheelMotion.SmoothAngle(current, target, deltaSeconds);
        if (result <= 55 || result >= 60)
            throw new Exception("SmoothAngle response time failed");

        // Test SmoothAngle shortest arc
        current = 359;
        target = 1;
        deltaSeconds = 0.005f;
        result = WheelMotion.SmoothAngle(current, target, deltaSeconds);
        if (result <= 359 && result >= 1)
            throw new Exception("SmoothAngle shortest arc failed");

        // Test frame-rate invariant
        current = 0;
        target = 90;
        deltaSeconds = 0.12f;
        float[] frameSteps = { 30, 60, 120, 144, 240 };
        float[] results = new float[frameSteps.Length];
        for (int i = 0; i < frameSteps.Length; i++)
        {
            float elapsed = 0f;
            float angle = current;
            while (elapsed < deltaSeconds)
            {
                float dt = Math.Min(1f / frameSteps[i], deltaSeconds - elapsed);
                angle = WheelMotion.SmoothAngle(angle, target, dt);
                elapsed += dt;
            }
            results[i] = angle;
            Equal(WheelMotion.SmoothAngle(current, target, deltaSeconds), angle,
                "Equal elapsed time at " + frameSteps[i] + " FPS", 0.002f);
        }
        for (int i = 1; i < frameSteps.Length; i++)
        {
            if (Math.Abs(results[i] - results[0]) > 0.002f)
                throw new Exception("Frame-rate invariant failed");
        }

        // Test no overshoot
        current = 0;
        target = 90;
        deltaSeconds = 0.12f / 300;
        for (int i = 0; i < 300; i++)
        {
            current = WheelMotion.SmoothAngle(current, target, deltaSeconds);
        }
        if (current > 90)
            throw new Exception("No overshoot failed");

        // Additional tests
        // 10ms response
        Equal(90f * (1f - (float)Math.Exp(-1f)), WheelMotion.SmoothAngle(0, 90, 0.01f), "10ms response", 0.002f);

        // Wrap symmetry
        Equal(WheelMotion.IconScale(359, 1), WheelMotion.IconScale(1, 359), "wrap", 0.002f);

        Console.WriteLine($"All {assertions} assertions passed.");
    }
}
