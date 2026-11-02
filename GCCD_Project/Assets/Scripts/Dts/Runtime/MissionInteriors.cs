using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class MissionDemo
    {
        Material signMaterial;
        void BuildInteriorTerminal()
        {
            foreach(var wall in MissionFloorPlan.Walls){
                bool exterior=Mathf.Abs((float)wall.X)>=33.9f||Mathf.Abs((float)wall.X)==12||wall.Z==-18||wall.Z==50;
                Box(exterior?"Terminal shell":"Interior partition",new Vector3((float)wall.X,(float)(wall.Bottom+wall.Height/2),(float)wall.Z),new Vector3((float)wall.Width,(float)wall.Height,(float)wall.Depth),exterior?Pbr("brick_wall_001"):Pbr("painted_plaster_wall"));
            }
            var floor=Pbr("concrete_floor_worn_001");
            var lamp=Mat("Fluorescent diffuser",new Color(.8f,.88f,.85f),0,.2f);lamp.EnableKeyword("_EMISSION");lamp.SetColor("_EmissionColor",new Color(2.3f,2.6f,2.4f));
            foreach(int side in new[]{-1,1}){
                Box("Interior concrete floor",new Vector3(side*23,.035f,16),new Vector3(22,.07f,68),floor);
                Box("Warehouse ceiling",new Vector3(side*23,5.35f,16),new Vector3(22.5f,.3f,68.5f),Pbr("painted_plaster_wall"));
                Box("Roof edge",new Vector3(side*23,5.6f,16),new Vector3(23,.25f,69),steel);
                for(int i=0;i<4;i++){
                    float z=-10+i*18;
                    Box("Corridor strip",new Vector3(side*22,.085f,z),new Vector3(.08f,.02f,14),paint,false);
                    foreach(float x in new[]{side*16f,side*22f,side*29f}){
                        Box("Ceiling light",new Vector3(x,5.04f,z),new Vector3(.55f,.10f,2.6f),lamp,false);
                        {var light=new GameObject("Interior light").AddComponent<Light>();light.transform.SetParent(level.transform);light.transform.position=new Vector3(x,3.8f,z);light.type=LightType.Point;light.range=11;light.intensity=2.5f;light.color=new Color(.82f,.89f,.90f);light.shadows=LightShadows.None;}
                    }
                    Box("Overhead beam",new Vector3(side*23,4.82f,z+6),new Vector3(22,.25f,.25f),steel,false);
                    Box("Service conduit",new Vector3(side*22.8f,4.7f,z),new Vector3(.16f,.16f,17),orange,false);
                    Box("Work desk",new Vector3(side*16,.85f,z-4),new Vector3(4,.16f,1.25f),trim);
                    foreach(float dx in new[]{-1.65f,1.65f})Box("Desk leg",new Vector3(side*16+dx,.42f,z-4),new Vector3(.12f,.84f,1.05f),steel);
                    Box("Console body",new Vector3(side*16,1.12f,z-4.25f),new Vector3(1.2f,.5f,.18f),trim);
                    Box("Console screen",new Vector3(side*16,1.12f,z-4.36f),new Vector3(.95f,.32f,.025f),glass,false);
                    RealModel("old_military_crate",new Vector3(side*31,.07f,z-4),1.3f,Pbr("old_military_crate"),true,Quaternion.identity);
                    RealModel("barrel_03",new Vector3(side*32,.07f,z+4),1.1f,Pbr("barrel_03"),true,Quaternion.identity);
                    Box("Storage rack base",new Vector3(side*32,.15f,z),new Vector3(1.5f,.15f,3.2f),steel);
                    foreach(float h in new[]{1f,2f,3f})Box("Storage shelf",new Vector3(side*32,h,z),new Vector3(1.5f,.12f,3.2f),steel);
                    foreach(float dz in new[]{-1.5f,1.5f})Box("Rack upright",new Vector3(side*32,1.6f,z+dz),new Vector3(.12f,3.2f,.12f),orange);
                    foreach(float dz in new[]{-2f,2f})Box("Entrance frame",new Vector3(side*11.8f,1.7f,z+dz),new Vector3(.22f,3.4f,.18f),orange,false);
                    ZoneSign((side<0?"A":"B")+"-0"+(i+1),new Vector3(side*11.78f,3.7f,z),Quaternion.Euler(0,side<0?90:-90,0));
                }
                ZoneSign("LOW CLEARANCE  /  C",new Vector3(side*29,1.55f,16.84f),Quaternion.identity);
                Box("Service hatch warning",new Vector3(side*29,1.3f,16.82f),new Vector3(1.8f,.10f,.05f),orange,false);
            }
        }
        void ZoneSign(string text,Vector3 position,Quaternion rotation)
        {
            var sign=new GameObject("Wayfinding "+text);sign.transform.SetParent(level.transform);sign.transform.SetPositionAndRotation(position,rotation);
            var mesh=sign.AddComponent<TextMesh>();mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.font.RequestCharactersInTexture("AB-0123456789LOW CLEARANCE / C",48);mesh.text=text;mesh.fontSize=48;mesh.characterSize=.10f;mesh.anchor=TextAnchor.MiddleCenter;mesh.color=new Color(.9f,.75f,.4f);
            if(!signMaterial){signMaterial=new Material(Resources.Load<Shader>("Realism/WorldLabel"));signMaterial.mainTexture=mesh.font.material.mainTexture;ownedVisuals.Add(signMaterial);}sign.GetComponent<MeshRenderer>().sharedMaterial=signMaterial;
        }
    }
}
