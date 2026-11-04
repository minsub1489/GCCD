using System;
using System.Collections.Generic;
using System.Linq;
namespace GCCD.Dts
{
    public sealed class MissionStrengths
    {
        public static readonly MissionStrengths Default = new MissionStrengths(80, 50, 20);
        public readonly int Near, Middle, Far;

        public MissionStrengths(int near, int middle, int far)
        {
            if (far < 1 || near > 100 || near < middle || middle < far)
                throw new ArgumentException("1~100 사이에서 가까움 ≥ 중간 ≥ 멀리 순으로 입력하세요.");
            Near = near;
            Middle = middle;
            Far = far;
        }

        public double AtDistance(double distance)
        {
            if (distance <= 21)
                return Middle + (Near - Middle) * (21 - Math.Max(6, distance)) / 15;
            return Far + (Middle - Far) * (36 - Math.Min(36, distance)) / 15;
        }
    }

    public static class MissionHapticEncoding
    {
        public const int DurationMilliseconds=200;
        public const string Version="x40-single-motor-custom-distance-v4";
        const int MiddleRow = 2;
        const double RowStepDegrees = 15;
        public static double CheckDistance(int level)
        {
            if(level<0||level>2)throw new ArgumentException("Select near, middle or far.");
            return new double[]{6,21,36}[level];
        }
        public static int[] Compose(IEnumerable<Threat> threats, MotorProfile profile, double observerHeight = 0, MissionStrengths strengths = null)
        {
            if (threats == null || profile == null) throw new ArgumentException("Profile and threats required.");
            Angles.Finite(observerHeight);
            profile.Validate(false);
            strengths = strengths ?? MissionStrengths.Default;
            var values = new int[40];
            foreach (var threat in threats)
            {
                double heightRatio = threat.Distance > 0 ? (threat.Y - observerHeight) / threat.Distance : 0;
                double elevation = Math.Asin(Math.Max(-1, Math.Min(1, heightRatio))) * 180 / Math.PI;
                // The X40 panels are row-major, with the top row first. Treat level threats as chest height.
                int row = MiddleRow - (int)Math.Round(elevation / RowStepDegrees, MidpointRounding.AwayFromZero);
                row = Math.Max(0, Math.Min(4, row));

                var motor = profile.Motors
                    .Where(m => m.Index % 20 / 4 == row)
                    .OrderBy(m => Angles.Difference(threat.Bearing, m.Bearing))
                    .ThenBy(m => Angles.Normalize(m.Bearing - threat.Bearing))
                    .First();
                double intensity = strengths.AtDistance(threat.Distance) * motor.Gain;
                int request = (int)Math.Round(Math.Min(100, intensity), MidpointRounding.AwayFromZero);
                values[motor.Index] = Math.Max(values[motor.Index], request);
            }
            return values;
        }
        public static int[] CheckFrame(int direction,int level,MotorProfile profile, MissionStrengths strengths = null)
        {
            if(direction<0||direction>3)throw new ArgumentException("Select a cardinal direction.");
            double distance=CheckDistance(level);return Compose(new[]{new Threat("HAPTIC_CHECK",true,true,true,0,0,0,distance,direction*90)},profile,strengths:strengths);
        }
    }
}
