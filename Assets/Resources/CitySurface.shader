Shader "Seabright/CitySurface"
{
    Properties {
        _Color("Surface color",Color)=(.6,.6,.6,1)
        _Pattern("Material pattern",Float)=0
        _Metallic("Metallic",Range(0,1))=0
        _Glossiness("Smoothness",Range(0,1))=.3
        _Scale("World scale",Float)=1
        _AlbedoMap("Scanned albedo",2D)="white"{} _NormalMap("Scanned normal",2D)="bump"{} _RoughMap("Scanned roughness",2D)="white"{}
        _TextureWeight("Scanned material blend",Range(0,1))=0 _TextureScale("Scanned repeat per meter",Float)=.5
    }
    SubShader {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0
        sampler2D _AlbedoMap,_NormalMap,_RoughMap;fixed4 _Color; half _Pattern,_Metallic,_Glossiness,_Scale,_TextureWeight,_TextureScale;
        struct Input {float3 worldPos;float3 worldNormal; INTERNAL_DATA};
        float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
        float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
        void vert(inout appdata_full v){v.tangent=float4(normalize(cross(v.normal,abs(v.normal.y)>.9?float3(0,0,1):float3(0,1,0))),1);}
        void surf(Input IN,inout SurfaceOutputStandard o){
            float3 wn=abs(WorldNormalVector(IN,float3(0,0,1)));
            float2 p=(wn.y>.7?IN.worldPos.xz:wn.x>.7?IN.worldPos.zy:IN.worldPos.xy)*_Scale;
            float fine=noise(p*26),broad=noise(p*.7),seam=0,variation=(fine-.5)*.12+(broad-.5)*.065;
            if(_Pattern>.5 && _Pattern<1.5){ // Bonded masonry, at a consistent physical brick size.
                float row=floor(p.y/.24);float2 brick=p/float2(.56,.24)+float2(fmod(row,2)*.5,0);float2 f=frac(brick);
                seam=max(1-smoothstep(.025,.07,min(f.x,1-f.x)),1-smoothstep(.06,.15,min(f.y,1-f.y)));
                variation+=(hash(floor(brick))-.5)*.18;
            }
            else if(_Pattern>1.5 && _Pattern<2.5){float row=floor(p.y/.3);float2 tile=p/float2(.42,.3)+float2(fmod(row,2)*.5,0);float2 f=frac(tile);seam=max(1-smoothstep(.01,.06,f.x),1-smoothstep(.04,.12,f.y));variation+=(hash(floor(tile))-.5)*.2;}
            else if(_Pattern>2.5 && _Pattern<3.5){variation=(fine-.5)*.24+(noise(p*4)-.5)*.1;float worn=smoothstep(.62,.75,noise(p*.27));variation+=worn*.08;}
            else if(_Pattern>3.5 && _Pattern<4.5){float2 slab=p/.85;float2 f=frac(slab);seam=1-smoothstep(.006,.023,min(min(f.x,1-f.x),min(f.y,1-f.y)));variation+=(hash(floor(slab))-.5)*.11;}
            else if(_Pattern>4.5 && _Pattern<5.5){float grainBand=sin(p.x*34+noise(p*1.2)*5);seam=1-smoothstep(.004,.024,frac(p.x/.25));variation+=grainBand*.055+(noise(p*float2(6,.4))-.5)*.19;}
            else if(_Pattern>5.5 && _Pattern<6.5){float2 cell=p/float2(1.2,.65);float2 f=frac(cell);seam=1-smoothstep(.008,.02,min(min(f.x,1-f.x),min(f.y,1-f.y)));variation+=(hash(floor(cell))-.5)*.06;}
            else if(_Pattern>6.5 && _Pattern<7.5){float row=floor(p.y/.5);float2 block=p/float2(1.15,.5)+float2(fmod(row,2)*.5,0);float2 f=frac(block);seam=1-smoothstep(.015,.045,min(min(f.x,1-f.x),min(f.y,1-f.y)));variation+=(hash(floor(block))-.5)*.2;}
            else if(_Pattern>7.5){variation+=sin(p.y*1.0)*.075;}
            float edgeTint=(_Pattern<1.5&&_Pattern>.5)?.18:-.23;
            o.Albedo=saturate(_Color.rgb*(1+variation+seam*edgeTint));
            o.Normal=normalize(float3((noise(p*18+float2(.05,0))-noise(p*18-float2(.05,0)))*.12,(noise(p*18+float2(0,.05))-noise(p*18-float2(0,.05)))*.12,1));
            float2 uv=p*_TextureScale;float3 scan=tex2D(_AlbedoMap,uv).rgb;
            o.Albedo=lerp(o.Albedo,scan*(.7+_Color.rgb*1.0)*(1+variation*.25),_TextureWeight);
            float3 scannedNormal=tex2D(_NormalMap,uv).rgb*2-1;o.Normal=normalize(lerp(o.Normal,float3(scannedNormal.xy*.42,max(.35,scannedNormal.z)),_TextureWeight));
            o.Metallic=_Metallic;o.Smoothness=saturate(_Glossiness+(fine-.5)*.1-seam*.15);o.Smoothness=lerp(o.Smoothness,1-tex2D(_RoughMap,uv).r,_TextureWeight*.8);o.Occlusion=1-seam*.14;o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
