using UnityEditor;
using UnityEngine;
using GCCD.Dts;
namespace GCCD.Editor
{
    public static class DtsPlayValidation
    {
        public static void Start(){DtsProjectBuilder.Generate();GameObject.Find("GCCD Controlled FPS Experiment").AddComponent<DtsPlayProbe>();EditorApplication.isPlaying=true;}
    }
}
