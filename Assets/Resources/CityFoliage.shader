Shader "Seabright/CityFoliage"
{
    Properties { _Color("Canopy color",Color)=(.19,.32,.13,1) }
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        fixed4 _Color;
        struct Input {float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,73.7)))*43758.5453);}
        float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
        void vert(inout appdata_full v){v.tangent=float4(normalize(cross(v.normal,abs(v.normal.y)>.9?float3(0,0,1):float3(0,1,0))),1);float strength=saturate(v.vertex.y*.08)*.055;v.vertex.x+=sin(_Time.y*1.0+v.vertex.z*.9+v.vertex.y)*strength;}
        void surf(Input IN,inout SurfaceOutputStandard o){float3 p=IN.worldPos;float leaf=noise(p*9),clump=noise(p*2.6);float variation=.77+leaf*.33+clump*.19;o.Albedo=_Color.rgb*variation;o.Normal=normalize(float3((noise(p*12+.11)-leaf)*.32,(noise(p*12-.15)-leaf)*.32,1));o.Smoothness=.11;o.Occlusion=.73+clump*.27;o.Alpha=1;}
        ENDCG
    }
    Fallback "Standard"
}
