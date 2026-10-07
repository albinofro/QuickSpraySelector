namespace QuickPickGraffiti
{
    // Last deliberately moved device owns aim; resting devices cannot undo a choice.
    internal sealed class WheelAimInput
    {
        private float leftAnchorX, leftAnchorY, rightAnchorX, rightAnchorY;
        private int owner;
        private bool suppressed;
        internal bool MouseActive { get { return owner == 3; } }

        internal void Reset()
        {
            leftAnchorX = leftAnchorY = rightAnchorX = rightAnchorY = 0f;
            owner = 0;
            suppressed = false;
        }

        internal bool Update(float leftX, float leftY, float rightX, float rightY,
            float mouseX, float mouseY, bool mouseMoved, bool discreteInput, out float x, out float y)
        {
            x = y = 0f;
            float leftMagnitude = leftX * leftX + leftY * leftY;
            float rightMagnitude = rightX * rightX + rightY * rightY;
            float leftDelta = Square(leftX - leftAnchorX) + Square(leftY - leftAnchorY);
            float rightDelta = Square(rightX - rightAnchorX) + Square(rightY - rightAnchorY);
            bool leftMoved = leftDelta > 0.0025f && leftMagnitude >= 0.16f;
            bool rightMoved = rightDelta > 0.0025f && rightMagnitude >= 0.16f;

            if (discreteInput)
            {
                leftAnchorX = leftX; leftAnchorY = leftY;
                rightAnchorX = rightX; rightAnchorY = rightY;
                suppressed = true;
                return false;
            }
            // Keep movement anchors until a real change, so slow turns accumulate.
            if (leftMoved || leftMagnitude < 0.0625f)
            { leftAnchorX = leftX; leftAnchorY = leftY; }
            if (rightMoved || rightMagnitude < 0.0625f)
            { rightAnchorX = rightX; rightAnchorY = rightY; }

            if (mouseMoved) { owner = 3; suppressed = false; }
            else if (leftMoved || rightMoved)
            {
                owner = rightMoved && (!leftMoved || rightDelta > leftDelta) ? 2 : 1;
                suppressed = false;
            }
            if (suppressed) return false;
            if (owner == 1 && leftMagnitude < 0.0625f)
                owner = rightMagnitude >= 0.16f ? 2 : 0;
            else if (owner == 2 && rightMagnitude < 0.0625f)
                owner = leftMagnitude >= 0.16f ? 1 : 0;

            if (owner == 1) { x = leftX; y = leftY; return true; }
            if (owner == 2) { x = rightX; y = rightY; return true; }
            if (owner == 3 && mouseMoved && mouseX * mouseX + mouseY * mouseY >= 0.0625f)
            { x = mouseX; y = mouseY; return true; }
            return false;
        }

        private static float Square(float value) { return value * value; }
    }
}