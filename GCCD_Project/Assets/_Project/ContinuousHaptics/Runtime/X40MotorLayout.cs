using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    public readonly struct VestMotorInfo
    {
        public readonly int Index, Row, RingColumn;
        public readonly bool IsFront;
        // X is wearer's left->right, Y is bottom->top. These are model coordinates, not measurements.
        public readonly Vector2 Position;
        public readonly float Azimuth;
        public VestMotorInfo(int index, bool front, int row, int column, int ring, float angle)
        { Index = index; IsFront = front; Row = row; RingColumn = ring;
            Position = new Vector2(column / 3f, 1f - row / 4f); Azimuth = angle; }
    }

    public static class X40MotorLayout
    {
        public const int Count = 40, Rows = 5, ColumnsPerPanel = 4, RingColumns = 8;
        // Official bHaptics legacy visualizer (ab77d37) and VRChatOSC Vest_old prefab:
        // panel indices run top->bottom, and left->right FROM THE WEARER'S PERSPECTIVE.
        // Front looks mirrored in an external front-view diagram. SDK2 combines front then back.
        // See SDK_AUDIT.md for sources, decoded coordinates, and hardware calibration limits.
        static readonly int[] TopRingIndices = { 2, 3, 23, 22, 21, 20, 0, 1 };
        static readonly VestMotorInfo[] Motors = Create();
        public static VestMotorInfo Get(int index) => Motors[index];
        public static int At(int ring, int row) => TopRingIndices[(ring % RingColumns + RingColumns) % RingColumns] + row * ColumnsPerPanel;
        static VestMotorInfo[] Create()
        {
            var motors = new VestMotorInfo[Count];
            for (int row = 0; row < Rows; row++)
                for (int ring = 0; ring < RingColumns; ring++)
                {
                    int index = At(ring, row);
                    motors[index] = new VestMotorInfo(index, index < 20, row, index % 4, ring, 22.5f + ring * 45);
                }
            return motors;
        }
    }
}
