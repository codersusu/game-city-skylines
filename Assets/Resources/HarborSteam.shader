Shader "Seabright/HarborSteam"
{
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.pos);return o;}
            fixed4 frag(v2f i):SV_Target{float2 p=i.uv*2-1;float alpha=saturate(1-dot(p,p));fixed4 c=i.color;c.a*=alpha*alpha;UNITY_APPLY_FOG(i.fogCoord,c);return c;}
            ENDCG
        }
    }
}
