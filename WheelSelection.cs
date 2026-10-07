using System;

namespace QuickPickGraffiti
{
    // Pure input state: pages stay fixed; only the selected sector and pointer move.
    internal sealed class WheelSelection
    {
        internal const int PageSize = 12;
        internal int Count { get; private set; }
        internal int SelectedIndex { get; private set; }
        internal int PageStart { get { return SelectedIndex / PageSize * PageSize; } }
        internal int VisibleCount { get { return Math.Min(PageSize, Count - PageStart); } }
        internal float PointerAngle { get; private set; }
        private bool stickActive;
        private bool stickSuppressed;
        private float suppressionAngle;

        internal WheelSelection(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException("count");
            Count = count;
        }

        internal void Step(int direction)
        {
            SelectedIndex = (SelectedIndex + direction + Count) % Count;
            SnapPointer();
        }

        internal void SelectIndex(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException("index");
            SelectedIndex = index;
            SnapPointer();
            stickActive = false;
            stickSuppressed = false;
        }

        internal void ChangePage(int direction)
        {
            int pages = (Count + PageSize - 1) / PageSize;
            int page = (SelectedIndex / PageSize + direction + pages) % pages;
            SelectedIndex = Math.Min(page * PageSize + SelectedIndex % PageSize, Count - 1);
            SnapPointer();
        }

        private void SnapPointer()
        {
            PointerAngle = (SelectedIndex - PageStart) * 360f / VisibleCount;
        }

        internal void UpdateStick(float x, float y, bool discreteInput)
        {
            float magnitudeSquared = x * x + y * y;
            if (magnitudeSquared < 0.0625f)
            {
                stickActive = false;
                stickSuppressed = false;
                return;
            }
            if (!stickActive && magnitudeSquared < 0.16f) return;

            float angle = (float)(Math.Atan2(x, y) * 180.0 / Math.PI);
            if (angle < 0f) angle += 360f;
            if (discreteInput)
            {
                stickActive = true;
                stickSuppressed = true;
                suppressionAngle = angle;
                return;
            }
            if (stickSuppressed)
            {
                if (Distance(angle, suppressionAngle) < 8f) return;
                stickSuppressed = false;
            }

            float sectorWidth = 360f / VisibleCount;
            float sectorCenter = (SelectedIndex - PageStart) * sectorWidth;
            if (!stickActive || Distance(angle, sectorCenter) > sectorWidth * 0.5f + 4f)
            {
                int sector = (int)Math.Floor(angle / sectorWidth + 0.5f) % VisibleCount;
                SelectedIndex = PageStart + sector;
            }
            stickActive = true;
            PointerAngle = angle;
        }

        private static float Distance(float a, float b)
        {
            float difference = Math.Abs(a - b) % 360f;
            return Math.Min(difference, 360f - difference);
        }
    }
}
