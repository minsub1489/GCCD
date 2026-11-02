using System;
using System.Linq;
namespace GCCD.Dts
{
    public static class X40Layout
    {
        // Official legacy visualizer: front indices increase screen-right to left,
        // back indices increase screen-left to right. Column zero is wearer's left.
        // The angular model approximates eight columns around the torso; not measured anatomy.
        public static MotorProfile Profile(bool mirrorFront=false,bool mirrorBack=false,bool swapPanels=false)
        {
            double[] front={292.5,337.5,22.5,67.5},back={247.5,202.5,157.5,112.5};
            return new MotorProfile{Version="x40-grid-v1-F"+(mirrorFront?1:0)+"B"+(mirrorBack?1:0)+"S"+(swapPanels?1:0),Provenance="bHaptics legacy VisualFeedback + visualizer front/back 4x5 index order; eight-column angular approximation, physical confirmation required",PhysicalLayoutVerified=false,
                Motors=Enumerable.Range(0,40).Select(i=>{bool isFront=(i<20)!=swapPanels;int column=i%4;if(isFront?mirrorFront:mirrorBack)column=3-column;return new Motor{Index=i,Bearing=(isFront?front:back)[column]};}).ToArray()};
        }
        public static int[][] Split(int[] values)
        {
            if(values==null||values.Length!=40||values.Any(v=>v<0||v>100))throw new ArgumentException("X40 requires 40 intensities in 0..100.");
            return new[]{values.Take(20).ToArray(),values.Skip(20).Take(20).ToArray()};
        }
    }
}
