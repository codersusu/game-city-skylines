using UnityEngine;

namespace Seabright
{
    public sealed partial class CityView
    {
        private void Road(MeshBatches b,CityTile t,Vector3 p)
        {
            bool n=IsRoad(t.X,t.Z+1),s=IsRoad(t.X,t.Z-1),e=IsRoad(t.X+1,t.Z),w=IsRoad(t.X-1,t.Z);int neighbors=(n?1:0)+(s?1:0)+(e?1:0)+(w?1:0);
            b.Box(M("sidewalk"),p+Vector3.up*.075f,new Vector3(10,.15f,10));b.Box(M("asphalt"),p+Vector3.up*.162f,new Vector3(6.8f,.06f,6.8f));
            if(n)b.Box(M("asphalt"),p+new Vector3(0,.162f,4.2f),new Vector3(6.8f,.06f,1.6f));if(s)b.Box(M("asphalt"),p+new Vector3(0,.162f,-4.2f),new Vector3(6.8f,.06f,1.6f));if(e)b.Box(M("asphalt"),p+new Vector3(4.2f,.162f,0),new Vector3(1.6f,.06f,6.8f));if(w)b.Box(M("asphalt"),p+new Vector3(-4.2f,.162f,0),new Vector3(1.6f,.06f,6.8f));
            if(neighbors<3){if(n||s){for(int i=-1;i<=1;i++)b.Box(M("yellow"),p+new Vector3(0,.205f,i*3.5f),new Vector3(.10f,.015f,1.6f));if(!e)b.Box(M("white"),p+new Vector3(2.95f,.205f,0),new Vector3(.08f,.012f,9.8f));if(!w)b.Box(M("white"),p+new Vector3(-2.95f,.205f,0),new Vector3(.08f,.012f,9.8f));}
                else if(e||w){for(int i=-1;i<=1;i++)b.Box(M("yellow"),p+new Vector3(i*3.5f,.205f,0),new Vector3(1.6f,.015f,.10f));b.Box(M("white"),p+new Vector3(0,.205f,2.95f),new Vector3(9.8f,.012f,.08f));b.Box(M("white"),p+new Vector3(0,.205f,-2.95f),new Vector3(9.8f,.012f,.08f));}}
            else {for(int direction=0;direction<4;direction++){if(!(direction==0?n:direction==1?e:direction==2?s:w))continue;Quaternion r=Quaternion.Euler(0,direction*90,0);for(int i=-3;i<=3;i++)b.Box(M("white"),p+r*new Vector3(i*.78f,.208f,4.16f),new Vector3(.43f,.015f,1.15f),direction*90);}
                for(int i=0;i<2;i++){Vector3 q=p+new Vector3(i==0?-3.7f:3.7f,.16f,i==0?3.7f:-3.7f);b.Cylinder(M("metal"),q,.07f,3.25f,6);b.Box(M("dark"),q+new Vector3(0,2.8f,0),new Vector3(.28f,.8f,.22f));b.Box(M("leafLight"),q+new Vector3(0,2.59f,-.13f),new Vector3(.15f,.17f,.025f));}}
            if(neighbors<=2&&(t.X+t.Z)%3==0){if(n||s){Tree(b,p+new Vector3(4.15f,.17f,1.5f),3.45f,t.Z);StreetLamp(b,p+new Vector3(-4.2f,.17f,-2.0f),0);}else{Tree(b,p+new Vector3(1.5f,.17f,-4.15f),3.45f,t.X);StreetLamp(b,p+new Vector3(-2.0f,.17f,4.2f),90);}}
            StreetDetails(b,t,p,n||s);
            // Expansion joints break up the pavement without using a separate texture per road.
            if(n||s)b.Box(M("stone"),p+new Vector3(4.1f,.156f,0),new Vector3(1.65f,.006f,.035f));if(e||w)b.Box(M("stone"),p+new Vector3(0,.156f,4.1f),new Vector3(.035f,.006f,1.65f));
        }
        private void Building(MeshBatches b,CityTile t,Vector3 p)
        {
            int variant=Mathf.FloorToInt(H(t.X,t.Z,13)*1000);float yaw=FrontYaw(t.X,t.Z);
            if(t.Kind==TileKind.Stadium){Stadium(b,p,variant);return;}
            if(t.Kind==TileKind.Park){Park(b,p,variant);return;}
            b.Box(M(t.Kind==TileKind.Residential?"grassLight":"paving"),p+Vector3.up*.035f,new Vector3(9.8f,.07f,9.8f));
            if(t.Level==0){Construction(b,p,t,variant);return;}
            if(t.Kind==TileKind.Power){Energy(b,p);return;}
            if(t.Kind==TileKind.Water){Water(b,p);return;}
            if(t.Kind==TileKind.Clinic){Clinic(b,p);return;}
            if(t.Kind==TileKind.Industrial){Industry(b,p,variant,yaw);return;}
            if(t.Kind==TileKind.Residential){House(b,p,t,variant,yaw);return;}
            if(t.Kind==TileKind.Commercial){Shops(b,p,t,variant,yaw);return;}
            ModernTower(b,p,t,variant,yaw);GardenEdge(b,p,variant);
        }

        private float FrontYaw(int x,int z)
        {
            for(int i=1;i<=2;i++){if(IsRoad(x,z-i))return 0;if(IsRoad(x+i,z))return 270;if(IsRoad(x,z+i))return 180;if(IsRoad(x-i,z))return 90;}return 0;
        }
        private void House(MeshBatches b,Vector3 p,CityTile t,int variant,float yaw)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);string path="Kenney/suburban/building-type-"+(char)('a'+variant%21);Asset(b,path,p+Vector3.up*.09f,6.6f,0,yaw);
            b.Box(M("sidewalk"),p+r*new Vector3(3.6f,.11f,-1.5f),new Vector3(1.5f,.05f,6.0f),yaw);b.Box(M("sidewalk"),p+r*new Vector3(0,.1f,-4),new Vector3(1.2f,.04f,2),yaw);
            Tree(b,p+r*new Vector3(-3.5f,.08f,2.8f),3.1f+variant%3*.35f,variant);
            b.Box(M("leaf"),p+r*new Vector3(-4.15f,.43f,-.7f),new Vector3(.55f,.8f,6.4f),yaw);b.Box(M("leafLight"),p+r*new Vector3(0,.38f,4.1f),new Vector3(8.0f,.7f,.55f),yaw);
            if(variant%3==0)Asset(b,"Kenney/cars/"+(variant%2==0?"sedan":"suv"),p+r*new Vector3(3.55f,.13f,-2.6f),2.9f,0,yaw);
            // Small material and street-level details remain legible when the player zooms in.
            for(int i=-4;i<=4;i++){b.Box(M("ivory"),p+r*new Vector3(i*.83f,.55f,4.48f),new Vector3(.12f,1.05f,.12f),yaw);}
            b.Box(M("ivory"),p+r*new Vector3(0,.65f,4.48f),new Vector3(7.4f,.10f,.10f),yaw);
            if(variant%3==1){b.Box(M("wood"),p+r*new Vector3(2.7f,.20f,2.8f),new Vector3(2,.22f,2.1f),yaw);Bench(b,p+r*new Vector3(2.7f,.3f,2.8f),yaw);}
            b.Cylinder(M("concrete"),p+r*new Vector3(-1.1f,.12f,-3.7f),.36f,.45f,12);LeafCrown(b,M("leaf"),p+r*new Vector3(-1.1f,.85f,-3.7f),new Vector3(.44f,.55f,.44f),variant);
            b.Box(M("wood"),p+r*new Vector3(-2.3f,.5f,-4.5f),new Vector3(.10f,.9f,.10f),yaw);b.Box(M("blue"),p+r*new Vector3(-2.3f,.95f,-4.5f),new Vector3(.35f,.28f,.4f),yaw);
        }
        private void Tower(MeshBatches b,Vector3 p,CityTile t,float height,int variant,float yaw)
        {
            float width=6.8f+(variant%3)*.32f,depth=6.4f+(variant%4)*.32f;
            bool glass=t.Kind==TileKind.Commercial&&variant%3!=0;Material wall=M(glass?"blueGlass":variant%3==0?"brick":variant%3==1?"ivory":"plaster");
            float podium=3.3f;b.Box(M(variant%2==0?"ivory":"brick"),p+Vector3.up*(podium*.5f+.08f),new Vector3(8.6f,podium,8.4f));
            // Towers step back from a human-scale retail podium, with parapets and roof equipment.
            b.Box(wall,p+Vector3.up*(height*.5f+podium),new Vector3(width,height,depth));
            int floors=Mathf.Max(2,Mathf.FloorToInt(height/2.65f));float floorH=height/floors;
            for(int f=0;f<floors;f++){
                float yy=podium+f*floorH+.8f;
                if(glass||f%3==0)b.Box(M(glass?"metal":"ivory"),p+Vector3.up*(podium+(f+1)*floorH),new Vector3(width+.1f,.16f,depth+.1f));
                for(int side=0;side<4;side++){
                    bool front=side<2;float length=front?width:depth;int columns=glass?4:3;
                    for(int w=0;w<columns;w++){float xx=-length*.5f+(w+.5f)*length/columns;float ww=length/columns*(glass?.86f:.65f),hh=floorH*(glass?.77f:.56f);Material window=(variant+w*3+f*7+side)%5==0?glassLit:M("glass");
                        if(front){float zz=(side==0?-1:1)*(depth*.5f+.012f);Vector3 a=p+new Vector3(xx-ww*.5f,yy,zz),d=p+new Vector3(xx+ww*.5f,yy+hh,zz);if(side==0)b.Quad(window,a,new Vector3(a.x,d.y,a.z),d,new Vector3(d.x,a.y,a.z));else b.Quad(window,new Vector3(d.x,a.y,a.z),d,new Vector3(a.x,d.y,a.z),a);}
                        else{float xxx=(side==2?-1:1)*(width*.5f+.012f);Vector3 a=p+new Vector3(xxx,yy,xx-ww*.5f),d=p+new Vector3(xxx,yy+hh,xx+ww*.5f);if(side==2)b.Quad(window,new Vector3(a.x,a.y,d.z),d,new Vector3(a.x,d.y,a.z),a);else b.Quad(window,a,new Vector3(a.x,d.y,a.z),d,new Vector3(a.x,a.y,d.z));}
                    }
                }
            }
            if(glass){for(int i=-1;i<=1;i++){float xx=i*width*.36f;b.Box(M("ivory"),p+new Vector3(xx,podium+height*.5f,-depth*.5f-.06f),new Vector3(.13f,height,.14f));b.Box(M("ivory"),p+new Vector3(xx,podium+height*.5f,depth*.5f+.06f),new Vector3(.13f,height,.14f));}}
            else if(variant%2==0){for(int f=1;f<floors;f++){float yy=podium+f*floorH;for(int side=-1;side<=1;side+=2){b.Box(M("ivory"),p+new Vector3(0,yy,side*(depth*.5f+.36f)),new Vector3(width*.76f,.16f,.9f));b.Box(M("glass"),p+new Vector3(0,yy+.38f,side*(depth*.5f+.72f)),new Vector3(width*.76f,.65f,.065f));}}}
            // Storefront glazing, colored awnings, cornice and a recessed entrance.
            for(int side=-1;side<=1;side+=2){for(int w=-1;w<=1;w++){b.Box(M("dark"),p+new Vector3(w*2.5f,1.35f,side*4.215f),new Vector3(1.98f,2.2f,.06f));b.Box((variant+w)%3==0?glassLit:M("glass"),p+new Vector3(w*2.5f,1.65f,side*4.26f),new Vector3(1.7f,1.25f,.025f));}b.Box(M(variant%2==0?"blue":"red"),p+new Vector3(0,2.8f,side*4.5f),new Vector3(7.7f,.17f,.9f));b.Box(M("ivory"),p+new Vector3(0,3.38f,side*4.14f),new Vector3(8.8f,.22f,.35f));}
            float roofY=podium+height;b.Box(M("roof"),p+Vector3.up*(roofY+.08f),new Vector3(width+.22f,.20f,depth+.22f));Rooftop(b,p+Vector3.up*(roofY+.19f),width,depth,variant);
            if(t.Level>=4&&variant%4==0){float h=4.5f;b.Box(M("metal"),p+Vector3.up*(roofY+h*.5f),new Vector3(1.6f,h,1.6f));b.Cylinder(M("white"),p+Vector3.up*(roofY+h),.055f,5,6);}
        }
        private void Rooftop(MeshBatches b,Vector3 p,float w,float d,int variant)
        {
            for(int s=-1;s<=1;s+=2){b.Box(M("ivory"),p+new Vector3(s*(w*.5f-.1f),.25f,0),new Vector3(.16f,.52f,d));b.Box(M("ivory"),p+new Vector3(0,.25f,s*(d*.5f-.1f)),new Vector3(w,.52f,.16f));}
            b.Box(M("metal"),p+new Vector3(-w*.2f,.43f,0),new Vector3(1.4f,.85f,1.5f));b.Cylinder(M("dark"),p+new Vector3(-w*.2f,.87f,0),.5f,.06f,10);
            b.Box(M("stone"),p+new Vector3(w*.15f,.4f,d*.23f),new Vector3(1.1f,.8f,1.3f));
            if(variant%3==0)Asset(b,"Kenney/industrial/solar-panel-landscape-group",p+new Vector3(w*.12f,.09f,-d*.17f),w*.43f,0,90);
            if(variant%5==0)Asset(b,"Kenney/industrial/detail-tank",p+new Vector3(-w*.2f,0,d*.24f),1.4f);
        }
        private void GardenEdge(MeshBatches b,Vector3 p,int variant)
        {
            for(int s=-1;s<=1;s+=2){Vector3 q=p+new Vector3(s*4.5f,.12f,4.4f);b.Box(M("ivory"),q+Vector3.up*.25f,new Vector3(.65f,.5f,.75f));Tree(b,q+Vector3.up*.5f,2.8f,variant+s);}
            if(variant%3==0)Bench(b,p+new Vector3(2.2f,.10f,-4.4f),0);
        }
        private void Industry(MeshBatches b,Vector3 p,int variant,float yaw)
        {
            Asset(b,"Kenney/industrial/building-"+(char)('a'+variant%20),p+Vector3.up*.08f,8.0f,6.0f+variant%5,yaw);
            if(variant%3==0){Asset(b,"Kenney/industrial/chimney-"+(variant%2==0?"large":"medium"),p+new Vector3(2.8f,.09f,2.8f),1.8f,12+variant%6);steamSources.Add(p+new Vector3(2.8f,12.1f+variant%6,2.8f));}
            if(variant%2==0)Asset(b,"Kenney/industrial/shipping-container-"+(char)('a'+variant%3),p+new Vector3(-2.8f,.1f,-3.7f),3.5f,0,90);
            b.Box(M("yellow"),p+new Vector3(3.5f,.14f,-2.5f),new Vector3(.12f,.02f,4));b.Box(M("yellow"),p+new Vector3(4.3f,.14f,-2.5f),new Vector3(.12f,.02f,4));
            if(variant%4==0)Asset(b,"Kenney/cars/truck",p+new Vector3(3.5f,.15f,-2.6f),3.4f,0,0);
        }
        private void Park(MeshBatches b,Vector3 p,int variant)
        {
            b.Box(M("grassLight"),p+Vector3.up*.04f,new Vector3(9.85f,.08f,9.85f));b.Box(M("sand"),p+Vector3.up*.10f,new Vector3(9.9f,.03f,1.25f));b.Box(M("sand"),p+Vector3.up*.11f,new Vector3(1.25f,.03f,9.9f));
            for(int i=0;i<4;i++){float x=(i%2==0?-1:1)*3,z=(i<2?-1:1)*3;Tree(b,p+new Vector3(x,.1f,z),3.5f+H(variant,i)*1.8f,variant+i);}
            if(variant%3==0){b.Cylinder(M("stone"),p+Vector3.up*.14f,1.55f,.38f,18);b.Cylinder(M("blue"),p+Vector3.up*.53f,1.35f,.03f,18);b.Cylinder(M("white"),p+Vector3.up*.55f,.12f,1.15f,8);}
            else{b.Box(M("brick"),p+new Vector3(2.6f,.2f,1.4f),new Vector3(2,.35f,.6f));b.Box(M("leafLight"),p+new Vector3(2.6f,.48f,1.4f),new Vector3(1.9f,.24f,.5f));}
            Bench(b,p+new Vector3(-2,.12f,.95f),180);Bench(b,p+new Vector3(2,.12f,-.95f),0);StreetLamp(b,p+new Vector3(-.9f,.12f,-3.8f),90);
        }
        private void Energy(MeshBatches b,Vector3 p)
        {
            b.Box(M("ivory"),p+new Vector3(-2.3f,1.7f,-2),new Vector3(3.8f,3.3f,3.4f));b.Box(M("roof"),p+new Vector3(-2.3f,3.4f,-2),new Vector3(4.0f,.2f,3.6f));
            Asset(b,"Kenney/industrial/solar-panel-landscape-group",p+new Vector3(1.8f,.14f,-1.7f),4.3f);Asset(b,"Kenney/industrial/windmill",p+new Vector3(-1.1f,.12f,2.7f),5.0f,21);
            for(int i=0;i<3;i++){b.Box(M("metal"),p+new Vector3(3.5f,.6f,1+i),new Vector3(1.3f,1.1f,.65f));b.Cylinder(M("dark"),p+new Vector3(3.5f,1.2f,1+i),.12f,.38f,6);}
        }
        private void Water(MeshBatches b,Vector3 p)
        {
            Asset(b,"Kenney/industrial/water-tower",p+new Vector3(-1,.1f,0),5.6f,13);b.Box(M("ivory"),p+new Vector3(2.7f,1.1f,-2.6f),new Vector3(3,2.1f,3.2f));b.Box(M("blue"),p+new Vector3(2.7f,2.2f,-2.6f),new Vector3(3.2f,.2f,3.4f));b.Cylinder(M("metal"),p+new Vector3(3.1f,.1f,2.7f),1.3f,2,12);Tree(b,p+new Vector3(-3.5f,.1f,-3.5f),3.3f,73);
        }
        private void Clinic(MeshBatches b,Vector3 p)
        {
            b.Box(M("ivory"),p+new Vector3(0,3.15f,0),new Vector3(7.4f,6.1f,7));b.Box(M("blueGlass"),p+new Vector3(0,3.6f,-3.51f),new Vector3(6.4f,3.5f,.04f));b.Box(M("roof"),p+new Vector3(0,6.3f,0),new Vector3(7.7f,.2f,7.3f));
            b.Box(M("white"),p+new Vector3(0,2.9f,-3.75f),new Vector3(8,.20f,1.2f));b.Box(M("red"),p+new Vector3(2.4f,5.2f,-3.57f),new Vector3(.37f,1.3f,.10f));b.Box(M("red"),p+new Vector3(2.4f,5.2f,-3.57f),new Vector3(1.3f,.37f,.10f));
            Asset(b,"Kenney/cars/ambulance",p+new Vector3(-2.7f,.12f,-4),3.3f,0,90);Tree(b,p+new Vector3(4.2f,.1f,3.2f),3.8f,50);Rooftop(b,p+new Vector3(0,6.42f,0),7.2f,6.8f,14);
        }
        private void Construction(MeshBatches b,Vector3 p,CityTile t,int variant)
        {
            string zone=t.Kind==TileKind.Residential?"zoneHomes":t.Kind==TileKind.Commercial?"zoneShops":t.Kind==TileKind.Industrial?"zoneIndustry":t.Kind==TileKind.HighResidential?"zoneHigh":"zoneOffice";
            Material identity=M(zone);
            b.Box(M("sand"),p+Vector3.up*.12f,new Vector3(8.2f,.20f,8.2f));
            for(int side=-1;side<=1;side+=2){b.Box(identity,p+new Vector3(side*4.38f,.16f,0),new Vector3(.34f,.16f,9.0f));b.Box(identity,p+new Vector3(0,.16f,side*4.38f),new Vector3(9,.16f,.34f));}
            // Distinct foundations and full-color street signs identify zoning before growth begins.
            if(t.Kind==TileKind.Residential){
                b.Box(M("concrete"),p+new Vector3(0,.28f,.6f),new Vector3(5.6f,.32f,5.3f));
                for(int side=-1;side<=1;side+=2)b.Box(M("brick"),p+new Vector3(side*2.65f,.65f,.6f),new Vector3(.24f,.65f,5.1f));
                b.Box(M("wood"),p+new Vector3(0,.52f,2.7f),new Vector3(3,.35f,.8f));
                b.Beam(identity,p+new Vector3(-1.4f,.43f,-2),p+new Vector3(0,.43f,-3.3f),.18f);b.Beam(identity,p+new Vector3(0,.43f,-3.3f),p+new Vector3(1.4f,.43f,-2),.18f);
            }else if(t.Kind==TileKind.Commercial){
                b.Box(M("concrete"),p+new Vector3(0,.27f,1.1f),new Vector3(7.1f,.3f,4.8f));
                for(int x=-1;x<=1;x++){b.Box(identity,p+new Vector3(x*2.4f,1.0f,-1.3f),new Vector3(.16f,1.6f,.16f));b.Box(M("white"),p+new Vector3(x*2.4f,.245f,-3.0f),new Vector3(.09f,.02f,1.5f));}
                b.Box(identity,p+new Vector3(0,1.8f,-1.3f),new Vector3(7.3f,.18f,.22f));
            }else if(t.Kind==TileKind.Industrial){
                b.Box(M("concrete"),p+new Vector3(0,.3f,.6f),new Vector3(7.4f,.4f,6));
                Asset(b,"Kenney/industrial/shipping-container-a",p+new Vector3(-2.5f,.55f,1.8f),3.8f,0,90);
                b.Cylinder(M("metal"),p+new Vector3(2.5f,.5f,2.7f),.6f,2.9f,12);b.Cylinder(identity,p+new Vector3(2.5f,2.5f,2.7f),.62f,.5f,12);
                for(int i=-3;i<=3;i++)b.Box(identity,p+new Vector3(i*.9f,.53f,-2.6f),new Vector3(.4f,.02f,.7f),30);
            }else{
                b.Box(M("concrete"),p+new Vector3(0,.3f,0),new Vector3(7,.35f,7));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)b.Box(M("metal"),p+new Vector3(x*2.8f,1.8f,z*2.8f),new Vector3(.18f,3.1f,.18f));
                for(int side=-1;side<=1;side+=2)b.Box(identity,p+new Vector3(0,3.3f,side*2.8f),new Vector3(5.8f,.18f,.18f));
            }
            b.Box(M("metal"),p+new Vector3(-3.2f,.87f,-3.9f),new Vector3(.10f,1.65f,.10f));
            b.Box(identity,p+new Vector3(-3.2f,1.6f,-3.9f),new Vector3(2.1f,1.2f,.12f));
            b.Box(M("white"),p+new Vector3(-3.2f,1.6f,-3.98f),new Vector3(.8f,.56f,.04f));
            if(t.Kind==TileKind.Residential){b.Beam(M("white"),p+new Vector3(-3.75f,1.85f,-3.99f),p+new Vector3(-3.2f,2.12f,-3.99f),.12f);b.Beam(M("white"),p+new Vector3(-3.2f,2.12f,-3.99f),p+new Vector3(-2.65f,1.85f,-3.99f),.12f);}
            else if(t.Kind==TileKind.Industrial)b.Box(M("white"),p+new Vector3(-2.75f,1.92f,-3.98f),new Vector3(.2f,.6f,.04f));
            else if(t.Kind==TileKind.Commercial)for(int i=-2;i<=2;i++)b.Box(identity,p+new Vector3(-3.2f+i*.21f,1.83f,-4.01f),new Vector3(.10f,.22f,.04f));
        }
    }
}
