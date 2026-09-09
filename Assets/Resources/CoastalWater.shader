Shader "Seabright/CoastalWater"
{
    Properties {
        _Color("Deep water",Color)=(0.028,0.21,0.27,1)
        _Shallow("Sunlit water",Color)=(0.11,0.42,0.40,1)
        _Glossiness("Smoothness",Range(0,1))=.89
        _WaveStrength("Ripples",Range(0,1))=.22
    }
    SubShader {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        fixed4 _Color,_Shallow;half _Glossiness,_WaveStrength;
        struct Input {float3 worldPos;float3 viewDir;};
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        void vert(inout appdata_full v){v.tangent=float4(1,0,0,1);v.vertex.y+=(sin(v.vertex.x*.11+v.vertex.z*.06+_Time.y*.45)+sin(v.vertex.z*.073-_Time.y*.38))*.09;}
        void surf(Input IN,inout SurfaceOutputStandard o){
            float2 p=IN.worldPos.xz;float t=_Time.y;
            float a=sin(dot(p,float2(.31,.21))+t*.55),b=sin(dot(p,float2(-.26,.38))-t*.41);
            float c=sin(dot(p,float2(1.14,.52))+t*1.17),d=sin(dot(p,float2(.51,-1.37))-t*.94);
            float detail=noise(p*2.1+float2(t*.17,-t*.09));
            o.Normal=normalize(float3(a*.12+b*.055+c*.052+(detail-.5)*.12,b*.10-a*.047+d*.042+(noise(p*2.6-float2(t*.11,t*.15))-.5)*.10,1));
            float coast=(38+2*sin((p.y/10+23.5)*.19)-24)*10;
            float distanceToShore=min(max(p.x-coast,0),max(-190-p.y,0)+step(-190,p.y)*10000);
            float shallow=1-saturate(distanceToShore/24);
            float froth=noise(p*1.7+float2(-t*.08,t*.06));
            float foam=(1-smoothstep(.6,3.0,distanceToShore))*smoothstep(.47,.75,froth)*(.48+.22*sin(t*.7+p.y*.08));
            float fresnel=pow(1-saturate(dot(normalize(IN.viewDir),o.Normal)),4);
            o.Albedo=lerp(_Color.rgb,_Shallow.rgb,shallow*.75+noise(p*.025)*.12)+fresnel*float3(.07,.1,.11);
            o.Albedo=lerp(o.Albedo,float3(.49,.64,.61),foam);
            o.Metallic=.27;o.Smoothness=_Glossiness-foam*.2;o.Occlusion=1;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
