using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GCCD.Dts
{
    // Mission-only rendering. These objects never carry colliders or participate in scoring.
    public sealed partial class MissionDemo
    {
        readonly List<ParticleSystem> visualParticles = new List<ParticleSystem>();
        readonly List<Transform> visualRotors = new List<Transform>();
        readonly List<LineRenderer> recoveryRings = new List<LineRenderer>();
        readonly List<LineRenderer> tracerPool = new List<LineRenderer>();
        readonly double[] tracerDeadlines = new double[8];
        Material particleGlow, particleDust, droneHull, droneSteel, droneRubber, droneAmber, droneCyan, droneLens;
        ParticleSystem impactSparks, impactDust, muzzleSmoke;
        float visualTime;
        bool visualPaused;
        int tracerIndex;

        Material Emissive(string name, Color baseColor, Color emission, float metallic = .3f)
        {
            var material = Mat(name, baseColor, metallic, .38f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            return material;
        }

        void BuildPresentation()
        {
            BuildEnhancedScenery();
            GradeTerminalLighting();
            particleGlow = new Material(Resources.Load<Shader>("VisualUpgrade/ParticleGlow"));
            particleGlow.name = "Mission additive particles";
            particleDust = new Material(Resources.Load<Shader>("VisualUpgrade/ParticleDust"));
            particleDust.name = "Mission dust particles";
            ownedVisuals.Add(particleGlow);
            ownedVisuals.Add(particleDust);
            EnsureDroneMaterials();

            // Soft dust, local to lit room ceilings. No random calls alter the mission seed.
            foreach (int side in new[]{-1,1})
            {
                for (int i=0; i<4; i++)
                {
                    var dust = Particles("Suspended warehouse dust", particleDust, 64, new Color(.73f,.8f,.79f,.09f), .024f, 16, .025f);
                    dust.transform.position = new Vector3(side*22,3.2f,-10+i*18);
                    var emission = dust.emission; emission.rateOverTime=3;
                    var shape=dust.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(8,2.6f,8);
                    var velocity=dust.velocityOverLifetime; velocity.enabled=true; velocity.x=.018f; velocity.y=-.025f; velocity.z=.012f;
                    dust.Play();
                }
            }
            impactSparks=Particles("Pooled impact sparks",particleGlow,96,new Color(4,1.4f,.25f,.9f),.045f,.28f,4);
            var sparkShape=impactSparks.shape; sparkShape.enabled=true; sparkShape.shapeType=ParticleSystemShapeType.Cone; sparkShape.angle=65; sparkShape.radius=.015f;
            var gravity=impactSparks.main;gravity.gravityModifier=.7f;
            impactDust=Particles("Pooled impact powder",particleDust,64,new Color(.58f,.56f,.5f,.24f),.14f,.6f,.5f);
            var dustShape=impactDust.shape; dustShape.enabled=true;dustShape.shapeType=ParticleSystemShapeType.Cone;dustShape.angle=40;dustShape.radius=.04f;
            muzzleSmoke=Particles("Pooled muzzle vapor",particleDust,48,new Color(.66f,.69f,.7f,.15f),.06f,.4f,.45f);
            var smokeShape=muzzleSmoke.shape;smokeShape.enabled=true;smokeShape.shapeType=ParticleSystemShapeType.Cone;smokeShape.angle=15;smokeShape.radius=.025f;

            // The existing muzzle activation/timing still owns the flash.
            muzzle.GetComponent<Renderer>().enabled=false;
            var flashMesh=new Mesh {name="Crossed muzzle flare"};
            flashMesh.vertices=new[]{new Vector3(-.7f,-.7f,0),new Vector3(.7f,-.7f,0),new Vector3(.7f,.7f,0),new Vector3(-.7f,.7f,0),new Vector3(0,-.6f,-.8f),new Vector3(0,.6f,-.8f),new Vector3(0,.6f,.8f),new Vector3(0,-.6f,.8f)};
            flashMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
            flashMesh.colors=new[]{new Color(5,2.1f,.4f),new Color(5,2.1f,.4f),new Color(5,2.1f,.4f),new Color(5,2.1f,.4f),new Color(4,1.2f,.2f),new Color(4,1.2f,.2f),new Color(4,1.2f,.2f),new Color(4,1.2f,.2f)};
            flashMesh.triangles=new[]{0,1,2,0,2,3,4,5,6,4,6,7};flashMesh.RecalculateBounds();ownedVisuals.Add(flashMesh);
            var flash=new GameObject("Muzzle flare planes");flash.transform.SetParent(muzzle.transform,false);flash.AddComponent<MeshFilter>().sharedMesh=flashMesh;flash.AddComponent<MeshRenderer>().sharedMaterial=particleGlow;
            for (int i=0;i<8;i++)
            {
                var line=Line("Pooled shot tracer",world.transform,particleGlow,.012f);
                line.positionCount=2;line.startColor=new Color(2,1.1f,.3f,.7f);line.endColor=new Color(.4f,.22f,.06f,0);line.enabled=false;tracerPool.Add(line);
            }
            var ringMat=Emissive("Recovery ring",new Color(.07f,.3f,.25f),new Color(.05f,.8f,.52f));
            foreach(var supply in supplies)
            {
                var ring=Line("Recovery locator ring",supply.transform,ringMat,.016f);
                ring.useWorldSpace=false;ring.loop=true;ring.positionCount=64;
                for(int i=0;i<64;i++){float angle=i*Mathf.PI*2/64;ring.SetPosition(i,new Vector3(Mathf.Sin(angle)*.62f,.025f,Mathf.Cos(angle)*.62f));}
                recoveryRings.Add(ring);
            }
            RenderSettings.fog=true;
        }

        void GradeTerminalLighting()
        {
            RenderSettings.ambientSkyColor=new Color(.32f,.40f,.46f);
            RenderSettings.ambientEquatorColor=new Color(.16f,.20f,.23f);
            RenderSettings.ambientGroundColor=new Color(.08f,.10f,.11f);
            RenderSettings.ambientIntensity=.72f;
            foreach(var lamp in level.GetComponentsInChildren<Light>())
            {
                if(lamp.name!="Interior light")continue;
                lamp.color=new Color(.67f,.81f,.88f);lamp.intensity=1.9f;
                if(Mathf.Abs(Mathf.Abs(lamp.transform.position.x)-22)<.1f&&lamp.transform.position.z<0)
                {
                    lamp.shadows=LightShadows.Soft;lamp.shadowStrength=.65f;
                    lamp.shadowResolution=LightShadowResolution.Medium;lamp.shadowBias=.035f;
                }
            }
        }

        ParticleSystem Particles(string name,Material material,int budget,Color color,float size,float life,float speed)
        {
            var go=new GameObject(name);go.transform.SetParent(world.transform,false);
            var system=go.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed=false;system.randomSeed=(uint)(137+visualParticles.Count*79);
            var main=system.main;main.playOnAwake=false;main.maxParticles=budget;main.startColor=color;main.startSize=size;main.startLifetime=life;main.startSpeed=speed;main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.Pause;
            var emission=system.emission;emission.rateOverTime=0;
            var shape=system.shape;shape.enabled=false;
            var fade=system.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            visualParticles.Add(system);return system;
        }

        LineRenderer Line(string name,Transform parent,Material material,float width)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.startWidth=width;line.endWidth=width;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }

        void EnsureDroneMaterials()
        {
            if(droneHull)return;
            droneHull = Mat("Drone · graphite ceramic", new Color(.15f,.19f,.2f), .62f,.38f);
            droneSteel = Mat("Drone · machined steel", new Color(.43f,.48f,.49f), .88f,.55f);
            droneRubber = Mat("Drone · rubber", new Color(.025f,.033f,.038f), .05f,.18f);
            droneAmber = Emissive("Drone · amber", new Color(.72f,.3f,.045f), new Color(.9f,.25f,.025f));
            droneCyan = Emissive("Drone · optic rim", new Color(.035f,.25f,.3f), new Color(.05f,.65f,.8f));
            droneLens = Emissive("Drone · lens", new Color(.04f,.075f,.09f), new Color(.04f,.24f,.3f),.75f);
        }

        bool BuildDronePresentation(GameObject enemy)
        {
            var prefab=Resources.Load<GameObject>("VisualUpgrade/Models/AegisSecurityDrone");
            if(!prefab)return false;
            EnsureDroneMaterials();
            var model=Instantiate(prefab,enemy.transform);model.name="Authored security drone";model.transform.localPosition=Vector3.zero;
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string slot=materials[i]?materials[i].name:"Hull";
                    materials[i]=slot.Contains("Steel")?droneSteel:slot.Contains("Rubber")?droneRubber:slot.Contains("Amber")?droneAmber:slot.Contains("Cyan")?droneCyan:slot.Contains("Lens")?droneLens:droneHull;
                }
                renderer.sharedMaterials=materials;
            }
            foreach(var child in model.GetComponentsInChildren<Transform>())if(child.name.StartsWith("Rotor"))visualRotors.Add(child);
            return true;
        }

        void PresentShot(Vector3 end)
        {
            if(!muzzleSmoke)return;
            muzzleSmoke.transform.SetPositionAndRotation(muzzle.transform.position,transform.rotation);muzzleSmoke.Play();muzzleSmoke.Emit(2);
            int i=tracerIndex++%tracerPool.Count;var line=tracerPool[i];line.SetPosition(0,muzzle.transform.position);line.SetPosition(1,end);line.enabled=true;tracerDeadlines[i]=Now+.045;
        }

        void PresentImpact(Vector3 point,Vector3 normal)
        {
            if(!impactSparks)return;
            Quaternion rotation=Quaternion.LookRotation(normal);
            impactSparks.transform.SetPositionAndRotation(point+normal*.02f,rotation);impactSparks.Play();impactSparks.Emit(9);
            impactDust.transform.SetPositionAndRotation(point+normal*.04f,rotation);impactDust.Play();impactDust.Emit(4);
        }

        void LateUpdate()
        {
            if(!world)return;
            bool stopped=paused||complete||!Application.isFocused;
            if(stopped)
            {
                // An explicit shot from editor tests can emit while the app is unfocused.
                // Pause currently playing systems even when the stopped state did not change.
                foreach(var system in visualParticles)if(system&&!system.isPaused)system.Pause();
                visualPaused=true;
                return;
            }
            if(visualPaused)
            {
                foreach(var system in visualParticles)if(system)system.Play();
                visualPaused=false;
            }
            visualTime+=Mathf.Min(Time.deltaTime,.05f);
            for(int i=visualRotors.Count-1;i>=0;i--){var rotor=visualRotors[i];if(!rotor){visualRotors.RemoveAt(i);continue;}rotor.Rotate(Vector3.up,(i%2==0?1:-1)*1800*Time.deltaTime,Space.Self);}
            foreach(var ring in recoveryRings)if(ring){float brightness=.7f+Mathf.Sin(visualTime*2)*.15f;ring.startColor=ring.endColor=new Color(brightness,brightness,brightness,1);}
            for(int i=0;i<tracerPool.Count;i++)if(tracerPool[i]&&Now>=tracerDeadlines[i])tracerPool[i].enabled=false;
        }
    }
}
