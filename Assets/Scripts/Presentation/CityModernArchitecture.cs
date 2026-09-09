using UnityEngine;

namespace Seabright
{
    public sealed partial class CityView
    {
        private void Shops(MeshBatches b,Vector3 p,CityTile t,int variant,float yaw)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);float h=BuildingHeight(t)-.3f;
            Material wall=M(variant%3==0?"brick":variant%3==1?"ivory":"plaster");
            b.Box(wall,p+r*new Vector3(0,h*.5f+.1f,.65f),new Vector3(8.1f,h,6.1f),yaw);
            b.Box(M("roof"),p+r*new Vector3(0,h+.2f,.65f),new Vector3(8.25f,.2f,6.25f),yaw);
            for(int i=-1;i<=1;i++){
                float xx=i*2.55f;
                b.Box(M("dark"),p+r*new Vector3(xx,1.43f,-2.42f),new Vector3(2.22f,2.58f,.10f),yaw);
                b.Box(M("glassOcean"),p+r*new Vector3(xx,1.4f,-2.50f),new Vector3(2.06f,2.38f,.06f),yaw);
                b.Box(M("metal"),p+r*new Vector3(xx,1.4f,-2.55f),new Vector3(.08f,2.5f,.06f),yaw);
                Material brand=M((variant+i+3)%3==0?"blue":(variant+i+3)%3==1?"red":"orange");
                b.Box(brand,p+r*new Vector3(xx,2.96f,-2.51f),new Vector3(2.26f,.43f,.18f),yaw);
                for(int j=-2;j<=2;j++)b.Box(M("ivory"),p+r*new Vector3(xx+j*.31f,2.97f,-2.62f),new Vector3(.20f,.10f,.035f),yaw);
                b.Box(brand,p+r*new Vector3(xx,2.61f,-3.04f),new Vector3(2.34f,.16f,1.16f),yaw);
                for(int j=-3;j<=3;j++)b.Box(M("ivory"),p+r*new Vector3(xx+j*.32f,2.706f,-3.04f),new Vector3(.14f,.03f,1.17f),yaw);
                if(h>5)for(int floor=0;floor<2;floor++){
                    float y=4.1f+floor*1.7f;if(y+.6f>h)continue;
                    b.Box(M("ivory"),p+r*new Vector3(xx,y,-2.44f),new Vector3(1.65f,1.25f,.18f),yaw);b.Box(M("glass"),p+r*new Vector3(xx,y+.06f,-2.55f),new Vector3(1.40f,1.02f,.04f),yaw);
                }
            }
            // An outdoor café and freestanding menu board make the commercial frontage readable.
            for(int i=0;i<2;i++){
                Vector3 q=p+r*new Vector3(-2.9f+i*2.2f,.15f,-4.05f);b.Cylinder(M("metal"),q,.055f,.66f,8);b.Cylinder(M("wood"),q+Vector3.up*.66f,.42f,.08f,16);
                for(int side=-1;side<=1;side+=2){b.Box(M("wood"),q+r*new Vector3(side*.62f,.4f,0),new Vector3(.35f,.08f,.38f),yaw);b.Box(M("metal"),q+r*new Vector3(side*.62f,.22f,0),new Vector3(.06f,.36f,.27f),yaw);}
            }
            b.Box(M("wood"),p+r*new Vector3(3.2f,.7f,-3.8f),new Vector3(.72f,1.1f,.12f),yaw);b.Box(M("dark"),p+r*new Vector3(3.2f,.76f,-3.89f),new Vector3(.58f,.78f,.04f),yaw);
            for(int i=0;i<3;i++)b.Box(M("white"),p+r*new Vector3(3.2f,.95f-i*.18f,-3.92f),new Vector3(.39f,.045f,.018f),yaw);
            Rooftop(b,p+r*new Vector3(0,h+.32f,.65f),7.6f,5.8f,variant);Tree(b,p+r*new Vector3(4.35f,.1f,3.9f),3.0f,variant);
        }
        private void ModernTower(MeshBatches b,Vector3 p,CityTile t,int variant,float yaw)
        {
            bool office=t.Kind==TileKind.Office;float total=BuildingHeight(t),podium=3.5f,tower=total-podium;
            Material glass=M(office?(variant%2==0?"glassDeep":"glassSilver"):(variant%2==0?"glassOcean":"glassSilver"));
            b.Box(M("stone"),p+Vector3.up*(podium*.5f+.1f),new Vector3(8.7f,podium,8.5f));
            for(int side=-1;side<=1;side+=2){
                b.Box(M("glassOcean"),p+new Vector3(0,1.7f,side*4.28f),new Vector3(7.6f,2.8f,.08f));
                b.Box(M("metal"),p+new Vector3(side*4.37f,1.7f,0),new Vector3(.09f,2.8f,7.7f));
                for(int x=-2;x<=2;x++)b.Box(M("ivory"),p+new Vector3(x*1.5f,1.7f,side*4.36f),new Vector3(.11f,3.0f,.14f));
            }
            b.Box(M("ivory"),p+new Vector3(0,3.22f,-4.50f),new Vector3(5.3f,.18f,1.0f));
            int floors=Mathf.Max(7,Mathf.FloorToInt(tower/2.9f));float floorH=tower/floors;
            float initialW=office?7.2f:7.55f,initialD=office?7.0f:6.9f;
            for(int floor=0;floor<floors;floor++){
                float progress=(float)floor/floors;
                float step=progress>.78f?.84f:progress>.45f&&variant%2==0?.94f:1;
                float w=initialW*step,d=initialD*step,y=podium+floor*floorH;
                b.Box(glass,p+new Vector3(0,y+floorH*.5f,0),new Vector3(w,floorH,d));
                b.Box(M(office?"metal":"ivory"),p+new Vector3(0,y+.055f,0),new Vector3(w+.14f,.11f,d+.14f));
                for(int side=-1;side<=1;side+=2){
                    for(int col=-2;col<=2;col++){
                        float xx=col*w/5;
                        b.Box(M(office?"metal":"ivory"),p+new Vector3(xx,y+floorH*.5f,side*(d*.5f+.045f)),new Vector3(office?.065f:.14f,floorH,.075f));
                        float zz=col*d/5;
                        b.Box(M(office?"metal":"ivory"),p+new Vector3(side*(w*.5f+.045f),y+floorH*.5f,zz),new Vector3(.075f,floorH,office?.065f:.14f));
                    }
                    if(!office){
                        b.Box(M("ivory"),p+new Vector3(0,y+.13f,side*(d*.5f+.31f)),new Vector3(w*.70f,.18f,.83f));
                        b.Box(M("glassOcean"),p+new Vector3(0,y+.63f,side*(d*.5f+.66f)),new Vector3(w*.70f,.88f,.065f));
                        b.Box(M("metal"),p+new Vector3(0,y+1.09f,side*(d*.5f+.66f)),new Vector3(w*.70f,.065f,.065f));
                        if(floor%3==0)for(int x=-1;x<=1;x+=2){b.Box(M("concrete"),p+new Vector3(x*w*.22f,y+.35f,side*(d*.5f+.34f)),new Vector3(.7f,.45f,.45f));b.Box(M("leaf"),p+new Vector3(x*w*.22f,y+.69f,side*(d*.5f+.34f)),new Vector3(.72f,.35f,.46f));}
                    }
                }
            }
            float roofW=initialW*.84f,roofD=initialD*.84f;
            b.Box(M("roof"),p+Vector3.up*(total+.10f),new Vector3(roofW+.22f,.20f,roofD+.22f));Rooftop(b,p+Vector3.up*(total+.22f),roofW,roofD,variant);
            if(office){
                b.Box(M("glassOcean"),p+new Vector3(0,total+1.8f,0),new Vector3(roofW*.55f,3.5f,roofD*.55f));
                b.Beam(M("metal"),p+new Vector3(-roofW*.51f,total+.35f,-roofD*.51f),p+new Vector3(roofW*.51f,total+4,-roofD*.51f),.15f);
                b.Beam(M("metal"),p+new Vector3(-roofW*.51f,total+.35f,roofD*.51f),p+new Vector3(roofW*.51f,total+4,roofD*.51f),.15f);
                b.Cylinder(M("metal"),p+new Vector3(roofW*.35f,total+3,0),.055f,6.5f,9);
                b.Cylinder(lamp,p+new Vector3(roofW*.35f,total+9.5f,0),.105f,.22f,10);
            }
            // A recessed entry, bike hoops and planted plaza give the towers a human-scale base.
            for(int i=0;i<3;i++){Vector3 q=p+new Vector3(-3.2f+i*.58f,.1f,-4.62f);b.Beam(M("metal"),q,q+Vector3.up*.65f,.045f);b.Beam(M("metal"),q+Vector3.up*.65f,q+new Vector3(.27f,.65f,0),.045f);b.Beam(M("metal"),q+new Vector3(.27f,.65f,0),q+Vector3.right*.27f,.045f);}
            Bollard(b,p+new Vector3(3.8f,.12f,-4.6f));Bollard(b,p+new Vector3(2.9f,.12f,-4.6f));
        }
    }
}
