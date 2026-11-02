using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace GCCD.Editor
{
    public static class RealismAssetPipeline
    {
        [MenuItem("GCCD/Distance Priority/Prepare Realistic Mission Assets")]
        public static void Prepare()
        {
            const string root="Assets/Resources/Realism";Directory.CreateDirectory(root+"/Materials");
            foreach(string path in Directory.GetFiles(root,"*",SearchOption.AllDirectories))
            {
                if(path.EndsWith(".meta"))continue;var ti=AssetImporter.GetAtPath(path) as TextureImporter;
                if(ti!=null){bool normal=Path.GetFileName(path).ToLowerInvariant().Contains("normal");bool linear=normal||path.Contains("packed-metal")||path.Contains("roughness")||path.Contains("Metallic");ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=!linear;ti.maxTextureSize=2048;ti.wrapMode=TextureWrapMode.Repeat;ti.mipmapEnabled=true;ti.anisoLevel=8;ti.filterMode=FilterMode.Trilinear;ti.SaveAndReimport();}
                var mi=AssetImporter.GetAtPath(path) as ModelImporter;if(mi!=null){mi.isReadable=true;mi.materialImportMode=ModelImporterMaterialImportMode.None;mi.SaveAndReimport();}
            }
            foreach(string asset in new[]{"asphalt_02","brick_wall_001","barrel_03","concrete_road_barrier","old_military_crate","akm"})
            {
                string prefix=root+"/"+asset+"/";bool gun=asset=="akm";
                var mat=new Material(Shader.Find("Standard"));mat.name=asset;mat.enableInstancing=true;
                mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+(gun?"Material_BaseColor.png":"albedo.jpg"));
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+(gun?"Material_Normal.png":"normal.jpg")));mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"packed-metal.png"));mat.EnableKeyword("_METALLICGLOSSMAP");mat.SetFloat("_GlossMapScale",1);mat.SetFloat("_BumpScale",.65f);
                string path=root+"/Materials/"+asset+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing){EditorUtility.CopySerialized(mat,existing);UnityEngine.Object.DestroyImmediate(mat);}else AssetDatabase.CreateAsset(mat,path);
            }
            var sky=new Material(Shader.Find("Skybox/Panoramic"));sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(root+"/industrial-sky.hdr"));sky.SetFloat("_Exposure",.9f);sky.SetFloat("_Rotation",130);
            string skyPath=root+"/Materials/sky.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(skyPath);if(old){EditorUtility.CopySerialized(sky,old);UnityEngine.Object.DestroyImmediate(sky);}else AssetDatabase.CreateAsset(sky,skyPath);
            AssetDatabase.SaveAssets();Debug.Log("DTS_REALISM_ASSETS_READY");
        }
        public static void PrepareAndProbe(){Prepare();DtsEasyValidation.StartMissionProbe();}
        public static void PrepareAndBuild(){Prepare();DtsProjectBuilder.ValidateAndBuildBoth();}
    }
}
