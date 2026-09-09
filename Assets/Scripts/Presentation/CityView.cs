using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seabright
{
    public sealed partial class CityView : MonoBehaviour
    {
        private CitySimulation sim;
        private readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        private readonly Dictionary<string,ImportedModel> models=new Dictionary<string,ImportedModel>();
        private GameObject cityRoot,overlayRoot,hover;
        private int lastHash=int.MinValue,overlay;
        private bool night;
        private struct LandscapeTree { public Vector3 Position; public float Height; public int Seed, X, Z; }
        private readonly List<LandscapeTree> editableTrees=new List<LandscapeTree>();
        private Material glassLit,lamp,lightPool;
        private sealed class ImportedModel {public Bounds bounds;public readonly List<Mesh> meshes=new List<Mesh>();public readonly List<Matrix4x4> matrices=new List<Matrix4x4>();public Material material;}
        private static float H(int x,int z,int salt=0){uint v=(uint)(x*73856093^z*19349663^salt*83492791);v^=v>>13;v*=1274126177;return (v&0xffff)/65535f;}
        private Material M(string key,Color color,float metallic=0,float smooth=.25f)
        {
            if(mats.TryGetValue(key,out var m))return m;
            m=new Material(Shader.Find("Standard")){name=key,color=color,enableInstancing=true};m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);mats.Add(key,m);return m;
        }
        private Material M(string key){return mats[key];}
        private static Color C(string hex){ColorUtility.TryParseHtmlString("#"+hex,out Color c);return c;}
        private void MakeMaterials()
        {
            M("grass",C("607751"));M("grassLight",C("6A8056"));M("grassDark",C("536D47"));M("asphalt",C("3A454B"),0,.2f);M("sidewalk",C("B1B0A4"));M("white",C("E9E7D8"));M("yellow",C("E1C884"));
            M("stone",C("86969A"));M("sand",C("C6B590"));M("wood",C("9A7856"));M("roof",C("525C61"));M("metal",C("647578"),.4f,.55f);M("dark",C("26393C"));
            M("ivory",C("D6D5C9"));M("plaster",C("C5C2B3"));M("brick",C("B17D61"));M("brickDark",C("916C58"));M("blueGlass",C("4E8795"),.42f,.81f);M("glass",C("6B9199"),.5f,.75f);
            M("leaf",C("3E643E"));M("leafLight",C("627C43"));M("leafDark",C("31583D"));M("bark",C("645847"));M("waterFoam",C("A4C5BC"),0,.5f);M("red",C("B75E4D"));M("blue",C("5994A0"));M("orange",C("C89258"));
            MakeDetailMaterials();
            glassLit=M("litWindows",C("71999F"),.35f,.72f);glassLit.EnableKeyword("_EMISSION");glassLit.SetColor("_EmissionColor",Color.black);
            var groundShader=Resources.Load<Shader>("CoastalGround");
            if(groundShader)foreach(string key in new[]{"grass","grassLight","grassDark"}){var color=M(key).color;M(key).shader=groundShader;M(key).color=color;BindSurface(M(key),"leafy_grass",.32f,1f);}
            var poolShader=Resources.Load<Shader>("StreetLightPool");
            if(poolShader){lightPool=new Material(poolShader){name="Warm street light pools"};mats.Add("street light pools",lightPool);}
            lamp=M("lamps",C("E6D5A1"),0,.5f);lamp.EnableKeyword("_EMISSION");lamp.SetColor("_EmissionColor",new Color(.2f,.14f,.05f));
        }
        public void Initialize(CitySimulation simulation)
        {
            sim=simulation;MakeMaterials();BuildLandscape();Refresh();BuildTraffic();
            M("hover",new Color(.31f,.89f,.85f));
            hover=new GameObject("Tile selection");hover.transform.SetParent(transform,false);
            var border=new MeshBatches();for(int i=0;i<2;i++){float v=i==0?-4.9f:4.9f;border.Box(M("hover"),new Vector3(v,.24f,0),new Vector3(.16f,.12f,9.95f));border.Box(M("hover"),new Vector3(0,.24f,v),new Vector3(9.95f,.12f,.16f));}border.Build("Selection border",hover.transform);hover.SetActive(false);
        }
        public void Refresh()
        {
            if(sim==null)return;int hash=17;unchecked{foreach(var t in sim.Tiles)hash=hash*31+(int)t.Kind*11+t.Level;}
            if(hash!=lastHash){lastHash=hash;steamSources.Clear();MeshBatches.Dispose(cityRoot);var b=new MeshBatches();foreach(var t in sim.Tiles){if(t.Kind==TileKind.Empty||!CitySimulation.IsFootprintAnchor(t))continue;Vector3 p=CitySimulation.World(t.X,t.Z);if(t.Kind==TileKind.Road)Road(b,t,p);else Building(b,t,p);}foreach(var tree in editableTrees){var tile=sim.Get(tree.X,tree.Z);if(tile!=null&&tile.Kind==TileKind.Empty)Tree(b,tree.Position,tree.Height,tree.Seed);}cityRoot=b.Build("City • batched architecture and streets",transform);RefreshTrafficPaths();}
            if(cityRoot && cityRoot.transform.Find("Property picking")==null) BuildPicking();
            RebuildOverlay();
        }
        private void BuildPicking()
        {
            var root=new GameObject("Property picking");root.transform.SetParent(cityRoot.transform,false);
            foreach(var t in sim.Tiles) {
                if(t.Kind==TileKind.Empty||t.Kind==TileKind.Road||!CitySimulation.IsFootprintAnchor(t))continue;
                float h=BuildingHeight(t)+(t.Kind==TileKind.Office?10:0);
                var go=new GameObject("Property "+t.X+", "+t.Z);go.layer=6;go.transform.SetParent(root.transform,false);go.transform.position=CitySimulation.World(t.X,t.Z);
                var lot=go.AddComponent<CityLot>();lot.X=t.X;lot.Z=t.Z;
                var collider=go.AddComponent<BoxCollider>();collider.center=new Vector3(0,h*.5f,0);float footprint=t.Kind==TileKind.Stadium?28f:8.3f;collider.size=new Vector3(footprint,h,footprint);
            }
        }
        private ImportedModel LoadModel(string path)
        {
            if(models.TryGetValue(path,out var cached))return cached;var prefab=Resources.Load<GameObject>(path);if(!prefab){models[path]=null;return null;}
            var model=new ImportedModel();bool first=true;
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true)){if(!mf.sharedMesh)continue;var mat=prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;model.meshes.Add(mf.sharedMesh);model.matrices.Add(mat);foreach(var v in mf.sharedMesh.vertices){Vector3 p=mat.MultiplyPoint3x4(v);if(first){model.bounds=new Bounds(p,Vector3.zero);first=false;}else model.bounds.Encapsulate(p);}}
            string[] split=path.Split('/');string pack=split.Length>1?split[1]:"suburban";
            bool windows=(pack=="suburban"||pack=="commercial"||pack=="industrial")&&path.Contains("building-");
            string materialKey="Kenney "+pack+(windows?" architecture":" props");
            model.material=M(materialKey,Color.white,0,.28f);
            var paletteShader=Resources.Load<Shader>("CityAssetPalette");
            if(paletteShader){model.material.shader=paletteShader;model.material.SetFloat("_Windows",windows?1:0);model.material.SetFloat("_Night",night?1:0);model.material.SetTexture("_WallMap",Resources.Load<Texture2D>("Surfaces/concrete_wall_008_diff"));model.material.SetTexture("_WallNormal",Resources.Load<Texture2D>("Surfaces/concrete_wall_008_normal"));}
            var tex=Resources.Load<Texture2D>("Kenney/"+pack+"/Textures/colormap");if(tex){tex.filterMode=FilterMode.Point;model.material.mainTexture=tex;}
            models[path]=model;return model;
        }
        private bool Asset(MeshBatches b,string path,Vector3 pos,float width,float height=0,float yaw=0)
        {
            var model=LoadModel(path);if(model==null||model.meshes.Count==0)return false;var size=model.bounds.size;float scale=width/Mathf.Max(.01f,Mathf.Max(size.x,size.z));float ys=height>0?height/Mathf.Max(.01f,size.y):scale;
            Matrix4x4 matrix=Matrix4x4.TRS(pos,Quaternion.Euler(0,yaw,0),new Vector3(scale,ys,scale))*Matrix4x4.Translate(new Vector3(-model.bounds.center.x,-model.bounds.min.y,-model.bounds.center.z));
            for(int i=0;i<model.meshes.Count;i++){var mesh=model.meshes[i];for(int j=0;j<mesh.subMeshCount;j++)b.For(model.material).Append(mesh,matrix*model.matrices[i],j);}return true;
        }
        private bool IsRoad(int x,int z){var t=sim.Get(x,z);return t!=null&&t.Kind==TileKind.Road;}
        private float Coast(float z){return (38+2*Mathf.Sin((z/10+23.5f)*.19f)-24)*10;}
        private void BuildLandscape()
        {
            var b=new MeshBatches();
            // A broad continuous peninsula keeps the playable grid grounded in a larger landscape.
            for(float z=-190;z<740;z+=10){float x0=Coast(z),x1=Coast(z+10);b.Quad(M("grass"),new Vector3(-900,-.05f,z),new Vector3(-900,-.05f,z+10),new Vector3(x1,-.05f,z+10),new Vector3(x0,-.05f,z));b.Quad(M("sand"),new Vector3(x0,-.05f,z),new Vector3(x1,-.05f,z+10),new Vector3(x1+4,-1.5f,z+10),new Vector3(x0+4,-1.5f,z));}
            b.Quad(M("sand"),new Vector3(-900,-.05f,-190),new Vector3(Coast(-190),-.05f,-190),new Vector3(Coast(-190)+4,-1.7f,-198),new Vector3(-900,-1.7f,-198));
            // Continuous world-space surface variation avoids tile-shaped color patches and seams.
            // Group mature trees into groves, leaving glades and a clear edge around the starter city.
            for(int i=0;i<6200;i++)
            {
                float x=-540+H(i,7)*680,z=-174+H(i,8)*680;if(x>Coast(z)-13)continue;
                int tx=Mathf.RoundToInt(x/10+23.5f),tz=Mathf.RoundToInt(z/10+23.5f);
                bool central=tx>=10&&tx<=36&&tz>=11&&tz<=37;if(central)continue;
                float grove=Mathf.PerlinNoise((x+923)*.012f,(z+615)*.012f);
                if(grove<.40f||H(i,21)>Mathf.Lerp(.2f,.96f,Mathf.InverseLerp(.4f,.7f,grove)))continue;
                // Keep the outside highway connection free of trees.
                if(Mathf.Abs(z-CitySimulation.World(CitySimulation.Gateway.x,CitySimulation.Gateway.y).z)<9&&x< -125)continue;
                float height=4.4f+H(i,31)*4.8f;
                if(tx>=0&&tx<CitySimulation.Size&&tz>=0&&tz<CitySimulation.Size)editableTrees.Add(new LandscapeTree{Position=new Vector3(x,0,z),Height=height,Seed=i,X=tx,Z=tz});else Tree(b,new Vector3(x,0,z),height,i);
            }
            BuildWaterfront(b);b.Build("Coast • terrain, forest and marina",transform);
            var water=new GameObject("Animated coastal water");water.transform.SetParent(transform,false);var wb=new MeshBatches();Material wm=new Material(Resources.Load<Shader>("CoastalWater") ?? Shader.Find("Standard")){name="Coastal water"};mats["ocean"]=wm;
            // Moderate tessellation lets long swells catch the light without expensive per-pixel depth work.
            for(int x=-900;x<1400;x+=40)for(int z=-1100;z<1100;z+=40)wb.Quad(wm,new Vector3(x,-1.1f,z),new Vector3(x,-1.1f,z+40),new Vector3(x+40,-1.1f,z+40),new Vector3(x+40,-1.1f,z));var wg=wb.Build("Sea",water.transform);foreach(var r in wg.GetComponentsInChildren<MeshRenderer>())r.shadowCastingMode=ShadowCastingMode.Off;
            BuildDistantHills();
        }
        private void BuildDistantHills()
        {
            var b=new MeshBatches();Material hill=M("distant hills",C("738878"));for(int k=0;k<18;k++){float x=-620+k*58,z=600+H(k,12)*140,h=40+H(k,18)*90;int n=16;Vector3 peak=new Vector3(x,h,z);for(int j=0;j<n;j++){float a=j*Mathf.PI*2/n,c=(j+1)*Mathf.PI*2/n;Vector3 a0=new Vector3(x+Mathf.Cos(a)*110,0,z+Mathf.Sin(a)*120),c0=new Vector3(x+Mathf.Cos(c)*110,0,z+Mathf.Sin(c)*120);b.For(hill).Triangle(a0,peak,c0);}}b.Build("Distant coastal hills",transform);
        }
        private void BuildWaterfront(MeshBatches b)
        {
            for(float z=-162;z<190;z+=4)
            {
                float x=Coast(z),next=Coast(z+4);
                // Shared vertices form a continuous curved promenade instead of overlapping axis-aligned slabs.
                b.Quad(M("sidewalk"),new Vector3(x-5,.35f,z),new Vector3(next-5,.35f,z+4),new Vector3(next+.9f,.35f,z+4),new Vector3(x+.9f,.35f,z));
                b.Quad(M("stone"),new Vector3(x+.9f,-1.4f,z),new Vector3(x+.9f,.35f,z),new Vector3(next+.9f,.35f,z+4),new Vector3(next+.9f,-1.4f,z+4));
                b.Quad(M("white"),new Vector3(x+.55f,.38f,z),new Vector3(next+.55f,.38f,z+4),new Vector3(next+.9f,.38f,z+4),new Vector3(x+.9f,.38f,z));
                b.Cylinder(M("metal"),new Vector3(x+.6f,.4f,z),.05f,.95f,6);b.Beam(M("metal"),new Vector3(x+.6f,1.33f,z),new Vector3(next+.6f,1.33f,z+4),.05f);
                if(((int)z+162)%16==0){Tree(b,new Vector3(x-3.5f,.36f,z),4.1f,(int)z);StreetLamp(b,new Vector3(x-4,.4f,z+2),90);Bench(b,new Vector3(x-1.3f,.4f,z-1),90);}
            }
            for(int k=0;k<4;k++){float z=-112+k*58,x=Coast(z);b.Box(M("wood"),new Vector3(x+19,.10f,z),new Vector3(39,.48f,3.4f));for(int n=0;n<9;n++){float px=x+3+n*4;b.Box(M("dark"),new Vector3(px,-.6f,z-1.3f),new Vector3(.4f,2,.4f));b.Box(M("dark"),new Vector3(px,-.6f,z+1.3f),new Vector3(.4f,2,.4f));b.Box(M("sand"),new Vector3(px,.36f,z),new Vector3(.06f,.018f,3.3f));}
                for(int n=0;n<4;n++){float px=x+10+n*8;b.Box(M("wood"),new Vector3(px,-.04f,z+5.0f),new Vector3(1.2f,.32f,7.6f));Asset(b,"Kenney/watercraft/boat-sail-"+(n%2==0?"a":"b"),new Vector3(px+3,-.8f,z+6.0f),5.9f,0,180);if(n%2==0)Asset(b,"Kenney/watercraft/boat-speed-a",new Vector3(px+2.8f,-.8f,z-5.6f),5.0f,0,0);}
            }
            // Waterfront green and civic plaza connect the core grid to the harbor.
            b.Box(M("sidewalk"),new Vector3(117,.05f,-45),new Vector3(42,.10f,3.4f));for(int i=0;i<6;i++){Tree(b,new Vector3(101+i*7,0,-39),3.5f,i+24);Tree(b,new Vector3(101+i*7,0,-51),3.5f,i+72);}
            Asset(b,"Kenney/watercraft/boat-fishing-small",new Vector3(200,-.8f,114),12,0,34);
            Asset(b,"Kenney/watercraft/ship-cargo-a",new Vector3(306,-.8f,158),32,0,10);
            for(int i=0;i<3;i++){float x=Coast(-167)+9+i*4;b.Box(M("waterFoam"),new Vector3(x,-.91f,-166+i*2),new Vector3(1,.015f,4));}
        }
        private void Tree(MeshBatches b,Vector3 p,float height,int seed)
        {
            DetailedTree(b,p,height,seed);
        }
        private void StreetLamp(MeshBatches b,Vector3 p,float yaw)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);b.Cylinder(M("metal"),p,.065f,4.6f,6);Vector3 end=p+r*new Vector3(1.15f,4.6f,0);b.Beam(M("metal"),p+Vector3.up*4.6f,end,.08f);b.Box(lamp,end,new Vector3(.58f,.12f,.26f),yaw);
            if(lightPool){Vector3 center=new Vector3(end.x,p.y+.085f,end.z);float radius=6.2f;b.Quad(lightPool,center+new Vector3(-radius,0,-radius),center+new Vector3(-radius,0,radius),center+new Vector3(radius,0,radius),center+new Vector3(radius,0,-radius));}
        }
        private void Bench(MeshBatches b,Vector3 p,float yaw)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);b.Box(M("wood"),p+Vector3.up*.42f,new Vector3(1.7f,.1f,.45f),yaw);b.Box(M("wood"),p+r*new Vector3(0,.75f,.23f),new Vector3(1.7f,.48f,.10f),yaw);for(int i=-1;i<=1;i+=2)b.Box(M("metal"),p+r*new Vector3(i*.58f,.22f,0),new Vector3(.08f,.42f,.4f),yaw);
        }
        public void SetNight(bool value)
        {
            night=value;
            if(lightPool)lightPool.SetFloat("_Night",night?1:0);
            foreach(var material in mats.Values)if(material.HasProperty("_Windows"))material.SetFloat("_Night",night?1:0);
            glassLit.color=night?C("D6C18A"):C("71999F");glassLit.SetColor("_EmissionColor",night?new Color(1.0f,.62f,.25f)*1.35f:Color.black);lamp.SetColor("_EmissionColor",night?new Color(1,.65f,.25f)*3:new Color(.2f,.14f,.05f));
        }
        public void ShowHover(int x,int z,Color color,bool visible,int span=1){if(!hover)return;hover.SetActive(visible);if(visible){hover.transform.localScale=new Vector3(span,1,span);hover.transform.position=CitySimulation.World(x,z);M("hover").color=color;}}
        public string SelectedDescription(CityTile t)
        {
            if(t==null)return "Select a city block";
            switch(t.Kind){case TileKind.Residential:return "Garden residences";case TileKind.Commercial:return "Neighborhood shops";case TileKind.HighResidential:return "Modern residential tower";case TileKind.Office:return "Office skyscraper";case TileKind.Stadium:return "Seabright Metropolitan Stadium";case TileKind.Industrial:return "Northbank works";case TileKind.Park:return "Public green space";case TileKind.Power:return "Renewable energy campus";case TileKind.Water:return "Municipal water tower";case TileKind.Clinic:return "Community health center";case TileKind.Road:return "Two-lane neighborhood street";default:return "Undeveloped land";}
        }
        public void SetOverlay(int value){overlay=value;RebuildOverlay();}
        private void RebuildOverlay()
        {
            MeshBatches.Dispose(overlayRoot);overlayRoot=null;if(overlay==0||sim==null)return;var b=new MeshBatches();
            foreach(var t in sim.Tiles){if(t.Kind==TileKind.Empty)continue;Color c=Color.white;
                if(overlay==1)c=ZoneColor(t.Kind);else if(overlay==2)c=t.Powered?C("63CAA8"):C("ED7970");else if(overlay==3)c=t.Watered?C("65BCE4"):C("ED7970");else if(overlay==4)c=t.Kind==TileKind.Road?Color.Lerp(C("65CE9F"),C("D88A62"),Mathf.Round(sim.RoadTraffic(t.X,t.Z)*10)/10):C("70868C");else c=Color.Lerp(C("D49670"),C("63CFA4"),Mathf.Round(Mathf.Clamp01(t.LandValue/100)*12)/12);
                string key="overlay "+ColorUtility.ToHtmlStringRGB(c);Material m=M(key,c);Vector3 p=CitySimulation.World(t.X,t.Z);float y=.32f;b.Box(m,p+new Vector3(0,y,-4.7f),new Vector3(9.7f,.11f,.45f));b.Box(m,p+new Vector3(0,y,4.7f),new Vector3(9.7f,.11f,.45f));b.Box(m,p+new Vector3(-4.7f,y,0),new Vector3(.45f,.11f,9.5f));b.Box(m,p+new Vector3(4.7f,y,0),new Vector3(.45f,.11f,9.5f));}
            overlayRoot=b.Build("City data overlay",transform);
        }
        private static Color ZoneColor(TileKind kind){switch(kind){case TileKind.HighResidential:return C("42B9A4");case TileKind.Office:return C("9297E8");case TileKind.Stadium:return C("EBCC8D");case TileKind.Residential:return C("70C98B");case TileKind.Commercial:return C("65AFE4");case TileKind.Industrial:return C("E9BD64");case TileKind.Park:return C("AAD969");case TileKind.Power:return C("CF9CE0");case TileKind.Water:return C("63D0DF");case TileKind.Clinic:return C("F5968A");default:return C("B2C6CC");}}
        private void OnDestroy(){foreach(var m in mats.Values)if(m)Destroy(m);}
    }
}
