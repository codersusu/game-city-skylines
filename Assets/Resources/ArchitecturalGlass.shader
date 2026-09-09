Shader "Seabright/ArchitecturalGlass"
{
    Properties {_Color("Glass tint",Color)=(.24,.44,.5,1) _Night("Night",Range(0,1))=0 _Windows("Windows",Float)=1 _Metallic("Metallic",Range(0,1))=.45 _Glossiness("Smoothness",Range(0,1))=.85}
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;half _Night,_Metallic,_Glossiness;
        struct Input {float3 worldPos;float3 worldNormal;float3 viewDir;};
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        void surf(Input IN,inout SurfaceOutputStandard o){
            float2 p=abs(IN.worldNormal.x)>.7?IN.worldPos.zy:IN.worldPos.xy;
            float2 cell=floor(p/float2(1.25,2.8));float room=hash(cell);
            float reflected=.88+.12*sin(p.y*.16+p.x*.27)+.07*sin(p.y*.6+p.x*.09);
            float blinds=step(.78,room)*(.82+.18*sin(p.y*30));
            float lit=step(.61,room)*(1-blinds*.35)*_Night;
            o.Albedo=_Color.rgb*reflected*(.82+room*.24)+blinds*.09;
            o.Emission=float3(.93,.66,.34)*lit*.58;
            o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Occlusion=1;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
