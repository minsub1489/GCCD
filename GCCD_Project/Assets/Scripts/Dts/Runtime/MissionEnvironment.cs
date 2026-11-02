using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace GCCD.Dts
{
    public sealed partial class MissionDemo
    {
        Material previousSky;AmbientMode previousAmbient;Color previousAmbientColor,previousSkyColor,previousEquatorColor,previousGroundColor,previousFogColor;
        float previousAmbientIntensity,previousReflection,previousFov,previousFarClip,previousFogDensity,previousFogStart,previousFogEnd;
        bool previousHdr,previousMsaa;FogMode previousFogMode;CameraClearFlags previousClear;
        readonly List<Light> disabledLights=new List<Light>();readonly List<UnityEngine.Object> ownedVisuals=new List<UnityEngine.Object>();
        Material steel,trim,paint,glass,orange;GameObject level;
        Material Mat(string name,Color color,float metallic,float smoothness)
        {var m=new Material(Shader.Find("Standard"));m.name=name;m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smoothness);m.enableInstancing=true;ownedVisuals.Add(m);return m;}
        Material Pbr(string name){var m=Resources.Load<Material>("Realism/Materials/"+name);if(!m)throw new InvalidOperationException("Realism material missing: "+name);return m;}
        void BuildEnvironment()
        {
            previousSky=RenderSettings.skybox;previousAmbient=RenderSettings.ambientMode;previousAmbientColor=RenderSettings.ambientLight;previousAmbientIntensity=RenderSettings.ambientIntensity;previousReflection=RenderSettings.reflectionIntensity;previousClear=cam.clearFlags;previousFov=cam.fieldOfView;
            previousSkyColor=RenderSettings.ambientSkyColor;previousEquatorColor=RenderSettings.ambientEquatorColor;previousGroundColor=RenderSettings.ambientGroundColor;
            previousFogColor=RenderSettings.fogColor;previousFogMode=RenderSettings.fogMode;previousFogDensity=RenderSettings.fogDensity;previousFogStart=RenderSettings.fogStartDistance;previousFogEnd=RenderSettings.fogEndDistance;
            previousFarClip=cam.farClipPlane;previousHdr=cam.allowHDR;previousMsaa=cam.allowMSAA;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.enabled){light.enabled=false;disabledLights.Add(light);}
            RenderSettings.skybox=Resources.Load<Material>("Realism/Materials/sky");RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.44f,.48f,.54f);RenderSettings.ambientEquatorColor=new Color(.25f,.27f,.3f);RenderSettings.ambientGroundColor=new Color(.14f,.13f,.12f);RenderSettings.ambientIntensity=.8f;RenderSettings.reflectionIntensity=.7f;
            RenderSettings.fogColor=new Color(.40f,.43f,.46f);RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.006f;cam.clearFlags=CameraClearFlags.Skybox;cam.allowHDR=true;cam.allowMSAA=true;cam.farClipPlane=250;
            level=new GameObject("PBR freight terminal");level.transform.SetParent(world.transform);
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.transform.SetParent(level.transform);sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(32,-38,0);sun.color=new Color(1,.88f,.74f);sun.intensity=1.35f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.85f;sun.shadowBias=.025f;
            steel=Mat("Aged zinc",new Color(.28f,.30f,.29f),.72f,.28f);trim=Mat("Blackened steel",new Color(.075f,.085f,.09f),.65f,.32f);paint=Mat("Faded lane marking",new Color(.72f,.62f,.35f),0,.12f);glass=Mat("Factory glazing",new Color(.13f,.22f,.25f),.65f,.85f);orange=Mat("Safety orange",new Color(.65f,.2f,.06f),.3f,.25f);
            Box("Weathered asphalt",new Vector3(0,-.25f,10),new Vector3(80,.5f,100),Pbr("asphalt_02"));
            BuildInteriorTerminal();
            for(int i=0;i<18;i++){Box("Center lane",new Vector3(0,.012f,-20+i*4),new Vector3(.13f,.018f,1.6f),paint);}
            foreach(int side in new[]{-1,1})Box("Lane edge",new Vector3(side*6,.01f,12),new Vector3(.10f,.015f,72),paint);
            for(int i=0;i<14;i++){float x=i%2==0?-7.6f:7.6f,z=-15+i*4.7f;RealModel("barrel_03",new Vector3(x,0,z),1.05f,Pbr("barrel_03"),true,Quaternion.Euler(i%3==0?90:0,i*43,0));}
            for(int i=0;i<6;i++)RealModel("concrete_road_barrier",new Vector3(i%2==0?-8.5f:8.5f,0,-9+i*10),2.6f,Pbr("concrete_road_barrier"),true,Quaternion.Euler(0,90,0));
            for(int i=0;i<7;i++)RealModel("old_military_crate",new Vector3(i%2==0?-8:8,0,-5+i*7.5f),1.3f,Pbr("old_military_crate"),true,Quaternion.Euler(0,i*17,0));
            for(int i=0;i<3;i++){var item=RealModel("old_military_crate",new[]{new Vector3(-28,.07f,-8),new Vector3(28,.07f,18),new Vector3(-17,.07f,42)}[i],.85f,Pbr("old_military_crate"),false,Quaternion.identity);item.name="Supply "+i;supplies.Add(item);var lamp=new GameObject("Recovery beacon").AddComponent<Light>();lamp.transform.SetParent(item.transform);lamp.transform.position=item.transform.position+Vector3.up*.6f;lamp.color=new Color(.4f,.8f,.7f);lamp.range=2;lamp.intensity=1;}
            Box("Freight terminal rear wall",new Vector3(0,4.5f,55),new Vector3(75,9,2),Pbr("brick_wall_001"));
            Box("Exit loading gate",new Vector3(0,3,53.8f),new Vector3(11,6,.3f),steel);
            Box("Rear parapet",new Vector3(0,9.1f,55),new Vector3(76,.4f,2.5f),trim);
            StaticBatchingUtility.Combine(level);
            var probe=new GameObject("Terminal reflections").AddComponent<ReflectionProbe>();probe.transform.SetParent(level.transform);probe.transform.position=new Vector3(0,3,8);probe.size=new Vector3(65,20,90);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;probe.intensity=.65f;probe.cullingMask=~(1<<30);probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;DynamicGI.UpdateEnvironment();probe.RenderProbe();
        }
        GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool collision=true)
        {
            // Per-face world-scale UVs avoid stretching photographed brick/asphalt.
            Vector3 h=size*.5f;var verts=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            Vector3[] normals={Vector3.forward,Vector3.back,Vector3.left,Vector3.right,Vector3.up,Vector3.down};
            foreach(var normal in normals){Vector3 u=normal.y!=0?Vector3.right:Vector3.Cross(Vector3.up,normal);Vector3 v=Vector3.Cross(normal,u);float hu=Mathf.Abs(u.x)*h.x+Mathf.Abs(u.z)*h.z;float hv=Mathf.Abs(v.x)*h.x+Mathf.Abs(v.y)*h.y+Mathf.Abs(v.z)*h.z;float hn=Mathf.Abs(normal.x)*h.x+Mathf.Abs(normal.y)*h.y+Mathf.Abs(normal.z)*h.z;int n=verts.Count;verts.Add(normal*hn-u*hu-v*hv);verts.Add(normal*hn+u*hu-v*hv);verts.Add(normal*hn+u*hu+v*hv);verts.Add(normal*hn-u*hu+v*hv);uv.Add(Vector2.zero);uv.Add(new Vector2(hu,0));uv.Add(new Vector2(hu,hv));uv.Add(new Vector2(0,hv));tri.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
            if(name=="Shutter slat"||name=="Window mullion"||name=="Clerestory glazing"||name=="Hazard jamb"||name=="Center lane"||name=="Lane edge")collision=false;
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateTangents();ownedVisuals.Add(mesh);
            var obj=new GameObject(name);obj.transform.SetParent(level.transform);obj.transform.position=position;obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;if(collision)obj.AddComponent<BoxCollider>().size=size;return obj;
        }
        GameObject RealModel(string asset,Vector3 position,float size,Material material,bool collider,Quaternion rotation)
        {
            string file=asset=="akm"?"weapon":"model";var prefab=Resources.Load<GameObject>("Realism/"+asset+"/"+file);if(!prefab)throw new InvalidOperationException("Downloaded model missing: "+asset);
            var root=new GameObject(asset);root.transform.SetParent(world.transform);var model=Instantiate(prefab,root.transform);model.transform.localPosition=Vector3.zero;model.transform.localRotation=rotation;
            var renderers=model.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(var r in renderers){b.Encapsulate(r.bounds);r.sharedMaterial=material;}
            float scale=size/Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));root.transform.localScale=Vector3.one*scale;
            b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);model.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);root.transform.position=position;
            if(collider){b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);var c=root.AddComponent<BoxCollider>();c.center=root.transform.InverseTransformPoint(b.center);c.size=b.size/scale;}
            return root;
        }
        void RestoreEnvironment()
        {
            RenderSettings.skybox=previousSky;RenderSettings.ambientMode=previousAmbient;RenderSettings.ambientLight=previousAmbientColor;RenderSettings.ambientIntensity=previousAmbientIntensity;RenderSettings.reflectionIntensity=previousReflection;
            RenderSettings.ambientSkyColor=previousSkyColor;RenderSettings.ambientEquatorColor=previousEquatorColor;RenderSettings.ambientGroundColor=previousGroundColor;
            RenderSettings.fogColor=previousFogColor;RenderSettings.fogMode=previousFogMode;RenderSettings.fogDensity=previousFogDensity;RenderSettings.fogStartDistance=previousFogStart;RenderSettings.fogEndDistance=previousFogEnd;
            foreach(var light in disabledLights)if(light)light.enabled=true;if(cam){cam.clearFlags=previousClear;cam.fieldOfView=previousFov;cam.farClipPlane=previousFarClip;cam.allowHDR=previousHdr;cam.allowMSAA=previousMsaa;}DynamicGI.UpdateEnvironment();
            foreach(var asset in ownedVisuals)if(asset)Destroy(asset);ownedVisuals.Clear();
        }
    }
}
