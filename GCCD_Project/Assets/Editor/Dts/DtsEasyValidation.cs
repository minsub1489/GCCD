using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GCCD.Dts;
namespace GCCD.Editor
{
    public static class DtsEasyValidation
    {
        public static void StartConnectionProbe(){DtsProjectBuilder.Generate();GameObject.Find("GCCD Controlled FPS Experiment").AddComponent<DtsConnectionProbe>();EditorApplication.isPlaying=true;}
        public static void StartMissionProbe(){DtsProjectBuilder.Generate();GameObject.Find("GCCD Controlled FPS Experiment").AddComponent<DtsMissionProbe>();EditorApplication.isPlaying=true;}
        public static void BuildAndVerify()
        {
            DtsValidation.Run();
            BhapticsSdk2EditorSetup.AddInitializerToCurrentScene();BhapticsSdk2EditorSetup.AddInitializerToCurrentScene();
            int count=Resources.FindObjectsOfTypeAll<GameObject>().Count(g=>g.scene.IsValid()&&g.name=="[bhaptics]");
            if(count!=1)throw new Exception("Inactive SDK initializer duplicated: "+count);
            Debug.Log("DTS_EASY_INITIALIZER_TEST_PASS");DtsProjectBuilder.BuildWindows();DtsProjectBuilder.BuildMac();
        }
    }
}
