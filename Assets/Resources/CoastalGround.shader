Shader "Seabright/CoastalGround"
{
    Properties { _Color("Meadow color",Color)=(.34,.44,.28,1) _AlbedoMap("Grass albedo",2D)="white"{} _NormalMap("Grass normal",2D)="bump"{} _RoughMap("Grass roughness",2D)="white"{} }
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _AlbedoMap,_NormalMap,_RoughMap;fixed4 _Color;
        struct Input {float3 worldPos;};
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        void vert(inout appdata_full v){v.tangent=float4(1,0,0,1);}
        void surf(Input IN,inout SurfaceOutputStandard o){
            float2 p=IN.worldPos.xz;
            float2 meadow=p+float2(noise(p*.025+float2(37,19)),noise(p*.027+float2(-17,61)))*16;
            float broad=noise(meadow*.031),patch=noise(meadow*.19),grain=noise(p*13),fine=noise(p*2.9);
            float variation=(broad-.5)*.13+(patch-.5)*.09+(grain-.5)*.18+(fine-.5)*.12;
            float dry=smoothstep(.57,.82,patch)*smoothstep(.35,.7,broad);
            float3 color=lerp(_Color.rgb,float3(.36,.33,.19),dry*.08);
            float2 uv=p*.32;float3 scan=tex2D(_AlbedoMap,uv).rgb;float luminosity=dot(scan,float3(.22,.7,.08));o.Albedo=color*(.65+luminosity*1.5)*(1+variation*.65);
            float3 normal=tex2D(_NormalMap,uv).rgb*2-1;o.Normal=normalize(float3(normal.xy*.4,max(.5,normal.z)));
            o.Metallic=0;o.Smoothness=(1-tex2D(_RoughMap,uv).r)*.25;o.Occlusion=.92+fine*.08;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
