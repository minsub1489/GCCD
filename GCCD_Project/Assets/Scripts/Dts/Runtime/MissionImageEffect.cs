using UnityEngine;
namespace GCCD.Dts
{
    [RequireComponent(typeof(Camera))]
    public sealed class MissionImageEffect:MonoBehaviour
    {
        Material material;
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!material)
            {
                var shader=Resources.Load<Shader>("Realism/FilmFinish");
                if(shader&&shader.isSupported)material=new Material(shader);
            }
            if(!material){Graphics.Blit(source,destination);return;}
            // Quarter-resolution highlight bloom. Temporary targets are returned even on failure.
            RenderTexture first=null,second=null;
            try
            {
                int width=Mathf.Max(1,source.width/4),height=Mathf.Max(1,source.height/4);
                var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.Default;
                first=RenderTexture.GetTemporary(width,height,0,format);
                second=RenderTexture.GetTemporary(width,height,0,format);
                first.filterMode=second.filterMode=FilterMode.Bilinear;
                Graphics.Blit(source,first,material,0);
                material.SetVector("_BlurDirection",new Vector4(1f/width,0,0,0));
                Graphics.Blit(first,second,material,1);
                material.SetVector("_BlurDirection",new Vector4(0,1f/height,0,0));
                Graphics.Blit(second,first,material,1);
                material.SetTexture("_BloomTex",first);
                Graphics.Blit(source,destination,material,2);
            }
            finally
            {
                if(first)RenderTexture.ReleaseTemporary(first);
                if(second)RenderTexture.ReleaseTemporary(second);
            }
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
