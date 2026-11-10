using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using GCCD.Dts;

namespace GCCD.Editor
{
    public static class VisualUpgradeValidation
    {
        public static void ValidateResources()
        {
            foreach(string name in new[]{"AegisSecurityDrone","IndustrialServiceUnit"})
            {
                var model=Resources.Load<GameObject>("VisualUpgrade/Models/"+name);
                Require(model!=null,"Custom model missing: "+name);
                Require(model.GetComponentsInChildren<Collider>().Length==0,"Visual model adds collision");
            }
            var drone=Resources.Load<GameObject>("VisualUpgrade/Models/AegisSecurityDrone");
            Require(drone.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Rotor_"))==4,"Four rotor pivots");
            Require(drone.GetComponentsInChildren<Renderer>().Length<=12,"Drone render budget");
            foreach(string asset in new[]{"industrial_wall_lamp","power_box_01"})
            {
                Require(Resources.Load<GameObject>("VisualUpgrade/FreeAssets/"+asset+"/model")!=null,"CC0 model missing");
                foreach(string map in new[]{"diff","nor_gl","metallic_smoothness","occlusion"})
                    Require(Resources.Load<Texture2D>("VisualUpgrade/FreeAssets/"+asset+"/"+asset+"_"+map+"_1k")!=null,"PBR map missing: "+map);
            }
            foreach(string name in new[]{"VisualUpgrade/ParticleGlow","VisualUpgrade/ParticleDust","Realism/FilmFinish"})
            {
                var shader=Resources.Load<Shader>(name);
                Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Shader failed: "+name);
            }
            Debug.Log("VISUAL_UPGRADE_RESOURCE_PASS");
        }
        public static void StartVisualProbe()
        {
            ValidateResources();
            EditorSceneManager.OpenScene(DtsProjectBuilder.ScenePath);
            UnityEngine.Object.FindFirstObjectByType<ExperimentPlatform>().gameObject.AddComponent<VisualUpgradeProbe>();
            EditorApplication.isPlaying=true;
        }
        public static void BuildBoth()
        {
            ValidateResources();
            Build(BuildTarget.StandaloneOSX,"Builds/VisualUpgrade/macOS/GCCD.app");
            Build(BuildTarget.StandaloneWindows64,"Builds/VisualUpgrade/Windows/GCCD.exe");
        }
        static void Build(BuildTarget target,string path)
        {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{DtsProjectBuilder.ScenePath},target=target,locationPathName=path,options=BuildOptions.None});
            Require(report.summary.result==BuildResult.Succeeded,"Build failed: "+target);
            Debug.Log("VISUAL_UPGRADE_BUILD_PASS "+target+" bytes="+report.summary.totalSize);
        }
        internal static void Require(bool condition,string message)
        {
            if(condition)return;
            Debug.LogError("VISUAL_UPGRADE_FAILED "+message);
            EditorApplication.Exit(2);
            throw new InvalidOperationException(message);
        }
    }

}
