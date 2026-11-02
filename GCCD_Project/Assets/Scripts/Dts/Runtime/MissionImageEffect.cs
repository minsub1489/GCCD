using UnityEngine;
namespace GCCD.Dts
{
    [RequireComponent(typeof(Camera))]
    public sealed class MissionImageEffect:MonoBehaviour
    {
        Material material;
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {if(!material){var shader=Resources.Load<Shader>("Realism/FilmFinish");if(shader)material=new Material(shader);}if(material)Graphics.Blit(source,destination,material);else Graphics.Blit(source,destination);}
        void OnDestroy(){if(material)Destroy(material);}
    }
}
