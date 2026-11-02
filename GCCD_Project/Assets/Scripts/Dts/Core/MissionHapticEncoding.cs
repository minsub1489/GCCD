using System;
using System.Collections.Generic;
namespace GCCD.Dts
{
    public static class MissionHapticEncoding
    {
        public const double Low=.20,High=.80;
        public const int DurationMilliseconds=200;
        public const string Version="distance-linear-20-80-v2";
        public static double CheckDistance(int level)
        {
            if(level<0||level>2)throw new ArgumentException("Select near, middle or far.");
            return new double[]{6,21,36}[level];
        }
        public static int[] Compose(IEnumerable<Threat> threats,MotorProfile profile)
        {return MotorComposer.Compose(threats,profile,Low,High);}
        public static int[] CheckFrame(int direction,int level,MotorProfile profile)
        {
            if(direction<0||direction>3)throw new ArgumentException("Select a cardinal direction.");
            double distance=CheckDistance(level);return Compose(new[]{new Threat("HAPTIC_CHECK",true,true,true,0,0,0,distance,direction*90)},profile);
        }
    }
}
