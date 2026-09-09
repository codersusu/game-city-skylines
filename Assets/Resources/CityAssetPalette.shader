Shader "Seabright/CityAssetPalette"
{
    Properties {
        _MainTex("Palette",2D)="white"{} _Color("Tint",Color)=(1,1,1,1)
        _Night("Night",Range(0,1))=0 _Windows("Building windows",Range(0,1))=0
        _WallMap("Scanned wall grain",2D)="white"{} _WallNormal("Scanned wall normal",2D)="bump"{}
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _MainTex,_WallMap,_WallNormal;fixed4 _Color;half _Night,_Windows;
        struct Input {float2 uv_MainTex;float3 worldPos;float3 worldNormal;INTERNAL_DATA};
        void vert(inout appdata_full v){v.tangent=float4(normalize(cross(v.normal,abs(v.normal.y)>.9?float3(0,0,1):float3(0,1,0))),1);}
        void surf(Input IN,inout SurfaceOutputStandard o){
            float3 source=tex2D(_MainTex,IN.uv_MainTex).rgb;float luminance=dot(source,float3(.2126,.7152,.0722));
            float green=saturate((source.g-source.r)*9)*saturate((source.g-source.b)*8);
            float3 color=lerp(luminance.xxx,source,.68);
            color=lerp(color,float3(.26,.37,.22)*(.58+luminance*.8),green*.88*(1-_Windows));
            float window=smoothstep(.055,.12,source.b-source.g)*smoothstep(.09,.18,source.b-source.r)*step(.3,source.b)*_Windows;
            float3 normal=abs(WorldNormalVector(IN,float3(0,0,1)));
            float2 projection=normal.y>.55?IN.worldPos.xz:normal.x>.55?IN.worldPos.zy:IN.worldPos.xy;
            float roof=step(.25,normal.y)*step(1.7,IN.worldPos.y)*(1-window)*_Windows;
            color=lerp(color,float3(.22,.27,.29)*(.68+luminance*.8),roof*green*.77);
            float row=floor(projection.y/.31);float2 tile=frac(projection/float2(.48,.31)+float2(fmod(row,2)*.5,0));
            float tileSeam=max(1-smoothstep(.014,.055,tile.x),1-smoothstep(.035,.11,tile.y));
            float3 scanned=tex2D(_WallMap,projection*.55).rgb;float scanLum=dot(scanned,float3(.22,.7,.08));
            color*=lerp(1,.80+scanLum*.75,_Windows*(1-window));color*=1-roof*tileSeam*.16;
            float room=step(.3,frac(sin(dot(floor(IN.worldPos.xz*.32),float2(12.9898,78.233)))*43758.5453));
            o.Albedo=lerp(color,float3(.29,.39,.43)*(.91+.09*sin(IN.worldPos.y*.7)),window*.74)*_Color.rgb;
            float3 bump=tex2D(_WallNormal,projection*.55).rgb*2-1;o.Normal=normalize(float3(bump.xy*.18*_Windows*(1-window),1));
            o.Emission=float3(1.0,.61,.26)*window*_Night*room*.8;
            o.Metallic=window*.15;o.Smoothness=lerp(.23,.72,window);o.Occlusion=1-roof*tileSeam*.12;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
