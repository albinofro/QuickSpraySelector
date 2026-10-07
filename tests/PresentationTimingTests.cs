using System;
using QuickPickGraffiti;

internal static class PresentationTimingTests
{
    private static void Near(float actual, float expected, string message)
    {
        if (Math.Abs(actual - expected) > .00001f) throw new Exception(message);
    }
    private static void Main()
    {
        foreach (float age in new[] { -1f, 0f, 3f, 9.99f, 10f })
            Near(PresentationTiming.BlurWeight(age), 0f, "Blur began before its ten-second delay");
        Near(PresentationTiming.BlurWeight(11f), 2f / 9f, "Main fade must be linear");
        Near(PresentationTiming.BlurWeight(12.5f), 5f / 9f, "Main fade must be linear");
        Near(PresentationTiming.BlurWeight(14f), 8f / 9f, "Curve must start only in the final second");
        Near(PresentationTiming.BlurWeight(14.5f), 35f / 36f, "Final second must ease out");
        Near(PresentationTiming.BlurWeight(15f), 1f, "Fade did not finish after five seconds");
        Near(PresentationTiming.BlurWeight(99f), 1f, "Fade exceeded full strength");
        float previous = 0f;
        for (int i = 0; i <= 1000; i++)
        {
            float elapsed = 5f * i / 1000f;
            float weight = PresentationTiming.BlurWeight(10f + elapsed);
            if (weight < previous || weight < 0f || weight > 1f) throw new Exception("Fade overshot or reversed");
            previous = weight;
        }
        float leftSlope = (PresentationTiming.BlurWeight(14f) - PresentationTiming.BlurWeight(13.99f)) / .01f;
        float rightSlope = (PresentationTiming.BlurWeight(14.01f) - PresentationTiming.BlurWeight(14f)) / .01f;
        if (Math.Abs(leftSlope - rightSlope) > .002f || PresentationTiming.BlurWeight(14.99f) < .99998f)
            throw new Exception("Tail must join smoothly and finish flat");
        Near(PresentationTiming.BlurWeight(0f), 0f, "New selector must reset its delay");
        var follower = new CameraMotionState();
        follower.SetTarget(-1f, 0f);
        Near(follower.X, 0f, "Changing highlights must not jump the camera");
        follower.Step(.1f);
        Near(follower.X, (float)(1.0 - 2.2 * Math.Exp(-1.2)), "Slightly snappier medium responsiveness");
        Near(follower.Y, 0f, "Left selection must not pull vertically");
        for (int i = 0; i < 60; i++)
        {
            float before = follower.X;
            follower.Step(1f / 60f);
            if (follower.X < before || follower.X > 1f) throw new Exception("Attachment must settle without a snap-back or bounce");
        }
        follower.Reset(); follower.SetTarget(1f, 0f); follower.Step(.5f);
        if (follower.X > -.95f) throw new Exception("Right selection must pull counterclockwise");
        follower.Reset(); follower.SetTarget(0f, 1f); follower.Step(.5f);
        if (follower.Y < .95f) throw new Exception("Top selection must pull the orbit upward");
        Near(follower.X, 0f, "Top selection must not pull horizontally");
        foreach (int fps in new[] { 30, 60, 143, 240 })
        {
            var sample = new CameraMotionState();
            sample.SetTarget(.6f, .8f);
            var single = new CameraMotionState();
            single.SetTarget(.6f, .8f); single.Step(.1f);
            for (int frame = 0; frame < fps; frame++) sample.Step(.1f / fps);
            Near(sample.X, single.X, "Attachment must be frame-rate independent");
            Near(sample.Y, single.Y, "Attachment must be frame-rate independent");
            sample.SetTarget(-.7f, -.3f); single.SetTarget(-.7f, -.3f);
            single.Step(.25f);
            for (int frame = 0; frame < fps; frame++) sample.Step(.25f / fps);
            Near(sample.X, single.X, "Retargeting must be frame-rate independent");
            Near(sample.Y, single.Y, "Retargeting must be frame-rate independent");
        }
        for (int i = 0; i < 1000; i++)
        {
            float before = follower.X;
            follower.SetTarget(i % 2 == 0 ? -1f : 1f, i % 3 == 0 ? -1f : 1f);
            Near(follower.X, before, "Retargeting must never teleport the orbit");
            follower.Step(.005f);
            if (float.IsNaN(follower.X) || Math.Abs(follower.X) > 1.00001f || Math.Abs(follower.Y) > 1.00001f)
                throw new Exception("Rapid selection must stay bounded");
        }
        float savedX = follower.X, savedY = follower.Y;
        follower.SetTarget(float.NaN, float.PositiveInfinity);
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) follower.Step(invalid);
        Near(follower.X, savedX, "Invalid time must not change the attachment");
        Near(follower.Y, savedY, "Invalid time must not change the attachment");
        follower.Reset(); follower.Step(10f);
        Near(follower.X, 0f, "Restore must clear positions and targets");
        Near(follower.Y, 0f, "Restore must clear positions and targets");
        follower.SetTarget(-3f, 3f); follower.Step(10f);
        Near(follower.X, 1f, "Attachment target must be clamped");
        Near(follower.Y, 1f, "Attachment target must be clamped");
        follower.Step(.1f); Near(follower.X, 1f, "Idle settling must retain the target");
        Console.WriteLine("10-second delay, linear fade/curved tail and frame-independent medium orbital attachment checks passed.");
    }
}
