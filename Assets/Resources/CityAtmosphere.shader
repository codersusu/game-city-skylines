Shader "Hidden/Seabright/CityAtmosphere"
{
    Properties {_MainTex("Image",2D)="white"{} _BloomTex("Bloom",2D)="black"{} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex,_BloomTex;
        float4 _Blur;
        float _Bloom;
        half4 threshold(v2f_img i):SV_Target {float3 c=tex2D(_MainTex,i.uv).rgb;float brightness=max(c.r,max(c.g,c.b));return float4(c*saturate((brightness-.82)/max(brightness,.001)),1);}
        half4 blur(v2f_img i):SV_Target {float2 d=_Blur.xy;float3 c=tex2D(_MainTex,i.uv).rgb*.227027;c+=tex2D(_MainTex,i.uv+d*1.384615).rgb*.316216;c+=tex2D(_MainTex,i.uv-d*1.384615).rgb*.316216;c+=tex2D(_MainTex,i.uv+d*3.230769).rgb*.070270;c+=tex2D(_MainTex,i.uv-d*3.230769).rgb*.070270;return float4(c,1);}
        half4 composite(v2f_img i):SV_Target {float3 c=tex2D(_MainTex,i.uv).rgb+tex2D(_BloomTex,i.uv).rgb*_Bloom;float l=dot(c,float3(.2126,.7152,.0722));c=lerp(l.xxx,c,1.035);float2 p=(i.uv-.5)*1.1;float vignette=1-dot(p,p)*.12;return float4(c*vignette,1);}
        ENDCG
        Pass {CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment threshold
            ENDCG}
        Pass {CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG}
        Pass {CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG}
    }
}
