Shader "Seabright/StreetLightPool"
{
    Properties {_Night("Night",Range(0,1))=0}
    SubShader
    {
        Tags {"Queue"="Transparent-10" "RenderType"="Transparent"}
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Night;
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xy;return o;}
            fixed4 frag(v2f i):SV_Target {float2 p=(i.uv-.5)*2;float falloff=pow(saturate(1-dot(p,p)),1.8);return float4(float3(.055,.037,.018)*falloff*_Night,0);}
            ENDCG
        }
    }
}
