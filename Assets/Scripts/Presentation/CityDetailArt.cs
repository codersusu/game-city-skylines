using UnityEngine;

namespace Seabright
{
    public sealed partial class CityView
    {
        private void MakeDetailMaterials()
        {
            M("zoneHomes",C("5EA77A"));M("zoneShops",C("4385B7"));M("zoneIndustry",C("CDAB55"));M("zoneHigh",C("36A28C"));M("zoneOffice",C("7873BB"));
            M("terracotta",C("965E48"));M("roofTile",C("657078"));M("concrete",C("AFB0A8"));M("paving",C("A6AAA4"));M("pitch",C("387940"));M("track",C("A57565"));M("seatBlue",C("2D788D"));M("seatLight",C("6FA5AD"));M("seatGold",C("D8B771"));
            M("glassOcean",C("568992"),.42f,.85f);M("glassSilver",C("879DA1"),.5f,.9f);M("glassDeep",C("386773"),.46f,.88f);
            var shader=Resources.Load<Shader>("CitySurface");
            if(shader){
                foreach(string key in new[]{"asphalt","sidewalk","sand","wood","roof","metal","stone","ivory","plaster","brick","brickDark","terracotta","roofTile","concrete","paving","pitch","track","bark"}){
                    Material m=M(key);Color color=m.color;float metal=m.GetFloat("_Metallic"),gloss=m.GetFloat("_Glossiness");m.shader=shader;m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",gloss);
                    int pattern=(key=="brick"||key=="brickDark")?1:(key=="terracotta"||key=="roofTile")?2:key=="asphalt"?3:(key=="sidewalk"||key=="paving")?4:(key=="wood"||key=="bark")?5:key=="metal"?6:key=="stone"?7:key=="pitch"?8:0;m.SetFloat("_Pattern",pattern);
                }
            }
            BindSurface(M("asphalt"),"asphalt_02",.33f,.78f);
            foreach(string key in new[]{"sidewalk","paving","ivory","plaster","concrete","stone"})BindSurface(M(key),"concrete_wall_008",.4f,.62f);
            foreach(string key in new[]{"brick","brickDark"})BindSurface(M(key),"red_brick_03",1f,.74f);
            BindSurface(M("sand"),"grass_path_2",.5f,.65f);
            shader=Resources.Load<Shader>("CityFoliage");if(shader)foreach(string key in new[]{"leaf","leafLight","leafDark"}){Color color=M(key).color;M(key).shader=shader;M(key).color=color;}
            shader=Resources.Load<Shader>("ArchitecturalGlass");if(shader)foreach(string key in new[]{"glass","blueGlass","glassOcean","glassSilver","glassDeep"}){Material m=M(key);Color color=m.color;m.shader=shader;m.color=color;}
        }
        private void BindSurface(Material m,string source,float scale,float blend)
        {
            m.SetTexture("_AlbedoMap",Resources.Load<Texture2D>("Surfaces/"+source+"_diff"));m.SetTexture("_NormalMap",Resources.Load<Texture2D>("Surfaces/"+source+"_normal"));m.SetTexture("_RoughMap",Resources.Load<Texture2D>("Surfaces/"+source+"_rough"));
            if(m.HasProperty("_TextureScale"))m.SetFloat("_TextureScale",scale);if(m.HasProperty("_TextureWeight"))m.SetFloat("_TextureWeight",blend);
        }
        private float BuildingHeight(CityTile t)
        {
            if(t.Kind==TileKind.Stadium)return 14;
            if(t.Level==0)return 4;
            if(t.Kind==TileKind.Power)return 21;if(t.Kind==TileKind.Water)return 13;if(t.Kind==TileKind.Park)return 3;if(t.Kind==TileKind.Clinic)return 7;if(t.Kind==TileKind.Industrial)return 15;
            if(t.Kind==TileKind.Residential)return 7;
            if(t.Kind==TileKind.Commercial)return 4.2f+Mathf.Min(t.Level,3)*2.1f;
            if(t.Kind==TileKind.HighResidential)return 17+t.Level*12+H(t.X,t.Z,13)*9;
            if(t.Kind==TileKind.Office)return 25+t.Level*14+H(t.X,t.Z,13)*12;
            return 7;
        }
        private void DetailedTree(MeshBatches b,Vector3 p,float height,int seed)
        {
            b.Cylinder(M("bark"),p,height*.035f,height*.65f,9,height*.014f);
            int crowns=height<4?4:6;
            for(int i=0;i<crowns;i++){
                float angle=i*2.399963f+seed*.61f;float spread=(i==0?.03f:.16f)*height;
                Vector3 q=p+new Vector3(Mathf.Cos(angle)*spread,height*(i==0?.74f:.59f+H(seed,i)*.1f),Mathf.Sin(angle)*spread);
                float radius=height*(.20f+H(seed,i,37)*.065f);
                if(i>0)b.Beam(M("bark"),p+Vector3.up*height*.38f,q,.09f*height/5);
                LeafCrown(b,M((seed+i)%4==0?"leafLight":(seed+i)%5==0?"leafDark":"leaf"),q,new Vector3(radius,radius*(1.15f+H(seed,i,5)*.25f),radius),seed+i*11);
            }
        }
        private void LeafCrown(MeshBatches b,Material material,Vector3 p,Vector3 radius,int seed)
        {
            var g=b.For(material);int start=g.vertices.Count;const int slices=9,rings=6;
            for(int y=0;y<=rings;y++){
                float a=Mathf.PI*y/rings;
                for(int x=0;x<=slices;x++){
                    float theta=x*Mathf.PI*2/slices;Vector3 unit=new Vector3(Mathf.Sin(a)*Mathf.Cos(theta),Mathf.Cos(a),Mathf.Sin(a)*Mathf.Sin(theta));
                    float lobe=1+.12f*Mathf.Sin(theta*3+seed)*Mathf.Sin(a*4+seed*.4f);
                    g.vertices.Add(p+Vector3.Scale(unit,radius)*lobe);g.normals.Add(new Vector3(unit.x/radius.x,unit.y/radius.y,unit.z/radius.z).normalized);g.uvs.Add(new Vector2((float)x/slices,(float)y/rings));
                }
            }
            for(int y=0;y<rings;y++)for(int x=0;x<slices;x++){int a=start+y*(slices+1)+x,c=a+slices+1;g.triangles.Add(a);g.triangles.Add(a+1);g.triangles.Add(c);g.triangles.Add(a+1);g.triangles.Add(c+1);g.triangles.Add(c);}
        }
        private void Bollard(MeshBatches b,Vector3 p){b.Cylinder(M("metal"),p,.065f,.75f,8);b.Cylinder(M("white"),p+Vector3.up*.55f,.068f,.08f,8);}
        private void StreetDetails(MeshBatches b,CityTile t,Vector3 p,bool vertical)
        {
            if((t.X+t.Z)%4!=0)return;float yaw=vertical?0:90;Quaternion r=Quaternion.Euler(0,yaw,0);
            b.Cylinder(M("metal"),p+r*new Vector3(1.8f,.204f,1.8f),.38f,.012f,16);
            for(int i=-1;i<=1;i++)b.Box(M("dark"),p+r*new Vector3(1.8f+i*.12f,.217f,1.8f),new Vector3(.04f,.008f,.42f),yaw);
            b.Box(M("metal"),p+r*new Vector3(3.05f,.208f,-2),new Vector3(.44f,.016f,.7f),yaw);
            for(int i=0;i<5;i++)b.Box(M("dark"),p+r*new Vector3(3.05f,.22f,-2.26f+i*.13f),new Vector3(.34f,.008f,.045f),yaw);
            b.Box(M("concrete"),p+r*new Vector3(-4.0f,.24f,3.2f),new Vector3(.75f,.17f,1.1f),yaw);
            b.Cylinder(M("metal"),p+r*new Vector3(-4.0f,.32f,3.2f),.21f,.7f,12);
            if((t.X+t.Z)%8==0){Bollard(b,p+r*new Vector3(3.85f,.17f,-3.5f));Bollard(b,p+r*new Vector3(3.85f,.17f,-4.6f));}
        }
    }
}
