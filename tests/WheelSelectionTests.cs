using System;
using System.Collections.Generic;
using QuickPickGraffiti;

internal static class WheelSelectionTests
{
    private static int assertions;
    private static void Equal(int expected, int actual, string name)
    {
        assertions++;
        if (expected != actual) throw new Exception(name + ": expected " + expected + ", got " + actual);
    }
    private static void Aim(WheelSelection wheel, double angle)
    {
        double radians = angle * Math.PI / 180.0;
        wheel.UpdateStick((float)Math.Sin(radians), (float)Math.Cos(radians), false);
    }
    public static void Main()
    {
        WheelSelection wheel = new WheelSelection(24);
        Aim(wheel, 0); Equal(0, wheel.SelectedIndex, "up");
        Aim(wheel, 90); Equal(3, wheel.SelectedIndex, "right");
        Aim(wheel, 180); Equal(6, wheel.SelectedIndex, "down");
        Aim(wheel, 270); Equal(9, wheel.SelectedIndex, "left");
        Aim(wheel, 359); Equal(0, wheel.SelectedIndex, "angle wrap");
        wheel.UpdateStick(0f, 0f, false); Equal(0, wheel.SelectedIndex, "release keeps selection");
        wheel.UpdateStick(0.1f, 0.1f, false); Equal(0, wheel.SelectedIndex, "drift ignored");
        Aim(wheel, 90);
        wheel.Step(1); wheel.UpdateStick(1f, 0f, true);
        wheel.UpdateStick(1f, 0f, false); Equal(4, wheel.SelectedIndex, "D-pad not undone by held stick");
        Aim(wheel, 92); Equal(4, wheel.SelectedIndex, "jitter cannot undo D-pad");
        Aim(wheel, 0); Equal(0, wheel.SelectedIndex, "intentional new aim resumes");
        Aim(wheel, 16); Equal(0, wheel.SelectedIndex, "boundary jitter stays put");
        Aim(wheel, 21); Equal(1, wheel.SelectedIndex, "past boundary hysteresis");
        Aim(wheel, 16); Equal(1, wheel.SelectedIndex, "reverse jitter stays put");
        Aim(wheel, 10); Equal(0, wheel.SelectedIndex, "intentional reverse crossing");
        wheel.Step(-1); Equal(23, wheel.SelectedIndex, "D-pad wraps full list");
        Equal(12, wheel.PageStart, "last page follows selection");
        wheel.Step(1); Equal(0, wheel.SelectedIndex, "wrap returns to first");
        wheel.ChangePage(1); Equal(12, wheel.SelectedIndex, "next page");
        Aim(wheel, 90); Equal(15, wheel.SelectedIndex, "analog stays on page");
        Equal(12, wheel.PageStart, "page layout stays fixed on analog selection");
        wheel.ChangePage(1); Equal(3, wheel.SelectedIndex, "page preserves slot");
        wheel.ChangePage(1); Equal(15, wheel.SelectedIndex, "page wraps");
        WheelSelection partial = new WheelSelection(14);
        for (int i = 0; i < 7; i++) partial.Step(1);
        partial.ChangePage(1); Equal(13, partial.SelectedIndex, "short page clamps slot");
        Equal(2, partial.VisibleCount, "short page count");
        Aim(partial, 0); Equal(12, partial.SelectedIndex, "short page up");
        Aim(partial, 180); Equal(13, partial.SelectedIndex, "short page down");
        partial.ChangePage(1); Equal(1, partial.SelectedIndex, "short page wrap preserves current slot");
        WheelSelection single = new WheelSelection(1);
        single.Step(-1); single.ChangePage(1); Aim(single, 180);
        Equal(0, single.SelectedIndex, "single item never out of range");
        wheel.SelectIndex(19); Equal(19, wheel.SelectedIndex, "recall on different page");
        Equal(12, wheel.PageStart, "recall opens correct page");
        Equal(210, (int)wheel.PointerAngle, "recall aims pointer");
        bool rejected = false;
        try { wheel.SelectIndex(24); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Equal(1, rejected ? 1 : 0, "invalid recall rejected");

        var saved = new Dictionary<string, string>();
        int writes = 0;
        Func<string, string> read = size => saved.ContainsKey(size) ? saved[size] : "";
        Action<string, string> write = (size, title) => { saved[size] = title; writes++; };
        var memory = new LastGraffitiStore(read, write);
        string[] available = { "First", "Second", "Third" };
        Equal(-1, memory.FindAvailable("M", available), "no history safely unavailable");
        Equal(0, writes, "opening does not record hover");
        memory.RecordPainted("M", "Second");
        memory.RecordPainted("L", "Third");
        memory.RecordPainted("XL", "First");
        Equal(1, memory.FindAvailable("M", available), "medium remembers medium");
        Equal(2, memory.FindAvailable("L", available), "large remembers large");
        Equal(0, memory.FindAvailable("XL", available), "extra large remembers extra large");
        Equal(-1, memory.FindAvailable("S", available), "unpainted category independent");
        Equal(-1, memory.FindAvailable("M", new[] { "First" }), "removed artwork safely unavailable");
        Equal(-1, memory.FindAvailable("M", null), "unavailable list safely unavailable");
        memory.RecordPainted("M", "Second");
        memory.RecordPainted("M", "");
        Equal(3, writes, "same piece and empty title do not overwrite history");
        var restarted = new LastGraffitiStore(read, write);
        Equal(1, restarted.FindAvailable("M", available), "history survives store restart");
        restarted.RecordPainted("M", "First");
        Equal(0, memory.FindAvailable("M", available), "later successful paint replaces medium");
        Equal(2, memory.FindAvailable("L", available), "later paint preserves other sizes");
        WheelAimInputTests.Run();
        WirkleMotionTests.Run();
        WheelMotionTests.Run();
        Console.WriteLine(assertions + " navigation and saved-choice assertions passed.");
    }
}
