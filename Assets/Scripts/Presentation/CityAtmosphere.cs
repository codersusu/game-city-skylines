using UnityEngine;

namespace Seabright
{
    // Built-in-pipeline finishing pass: restrained highlight bloom and a soft lens vignette.
    [RequireComponent(typeof(Camera))]
    public sealed class CityAtmosphere : MonoBehaviour
    {
        Material material;
        public float Bloom=.23f;
        void OnEnable(){var shader=Resources.Load<Shader>("CityAtmosphere");if(shader&&shader.isSupported)material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};}
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(!material){Graphics.Blit(source,destination);return;}
            int width=Mathf.Max(1,source.width/4),height=Mathf.Max(1,source.height/4);
            var a=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.DefaultHDR);var b=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.DefaultHDR);a.filterMode=FilterMode.Bilinear;b.filterMode=FilterMode.Bilinear;
            Graphics.Blit(source,a,material,0);
            material.SetVector("_Blur",new Vector4(1f/width,0,0,0));Graphics.Blit(a,b,material,1);
            material.SetVector("_Blur",new Vector4(0,1f/height,0,0));Graphics.Blit(b,a,material,1);
            material.SetTexture("_BloomTex",a);material.SetFloat("_Bloom",Bloom);Graphics.Blit(source,destination,material,2);
            RenderTexture.ReleaseTemporary(a);RenderTexture.ReleaseTemporary(b);
        }
        void OnDisable(){if(material)Destroy(material);}
    }
}
