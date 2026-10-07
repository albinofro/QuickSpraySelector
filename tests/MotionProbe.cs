using System;
using QuickPickGraffiti;
class MotionProbe { static int Main() { float a=WheelMotion.IconScale(1,359),b=WheelMotion.IconScale(359,1); Console.WriteLine("Wrap symmetry: "+a+" vs "+b); return Math.Abs(a-b)<.00001f ? 0:1; } }
