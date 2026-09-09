using UnityEngine;

namespace Seabright
{
    public sealed partial class CityView
    {
        private void Stadium(MeshBatches b,Vector3 p,int variant)
        {
            b.Box(M("paving"),p+Vector3.up*.07f,new Vector3(28.7f,.14f,28.7f));
            b.Box(M("dark"),p+Vector3.up*.17f,new Vector3(20.4f,.08f,14.0f));
            b.Box(M("track"),p+Vector3.up*.24f,new Vector3(18.8f,.07f,12.8f));
            b.Box(M("pitch"),p+Vector3.up*.29f,new Vector3(16.6f,.08f,10.1f));
            for(int i=0;i<10;i++)b.Box(M(i%2==0?"pitch":"grassDark"),p+new Vector3(-7.47f+i*1.66f,.339f,0),new Vector3(1.66f,.012f,10.1f));
            // Regulation-style markings: touch lines, halfway line, centre circle, penalty boxes and goals.
            for(int side=-1;side<=1;side+=2){
                b.Box(M("white"),p+new Vector3(0,.355f,side*4.62f),new Vector3(15.4f,.018f,.065f));
                b.Box(M("white"),p+new Vector3(side*7.67f,.355f,0),new Vector3(.065f,.018f,9.30f));
                b.Box(M("white"),p+new Vector3(side*5.45f,.36f,0),new Vector3(.065f,.018f,5.6f));
                for(int z=-1;z<=1;z+=2)b.Box(M("white"),p+new Vector3(side*6.55f,.36f,z*2.8f),new Vector3(2.25f,.018f,.065f));
                Vector3 goal=p+new Vector3(side*7.86f,.35f,0);
                for(int z=-1;z<=1;z+=2){b.Beam(M("white"),goal+Vector3.forward*z*1.2f,goal+new Vector3(0,1.5f,z*1.2f),.07f);b.Beam(M("white"),goal+new Vector3(0,1.5f,z*1.2f),goal+new Vector3(side*.7f,.1f,z*1.2f),.04f);}
                b.Beam(M("white"),goal+new Vector3(0,1.5f,-1.2f),goal+new Vector3(0,1.5f,1.2f),.07f);
                for(int j=-5;j<=5;j++)b.Beam(M("white"),goal+new Vector3(side*.7f,.08f,j*.23f),goal+new Vector3(side*.7f,1.3f,j*.23f),.018f);
                for(int j=1;j<=5;j++)b.Beam(M("white"),goal+new Vector3(side*.7f,j*.23f,-1.2f),goal+new Vector3(side*.7f,j*.23f,1.2f),.018f);
            }
            b.Box(M("white"),p+new Vector3(0,.36f,0),new Vector3(.065f,.018f,9.3f));
            const int circleSegments=48;for(int i=0;i<circleSegments;i++){float a=i*Mathf.PI*2/circleSegments,c=(i+1)*Mathf.PI*2/circleSegments;b.Beam(M("white"),p+new Vector3(Mathf.Cos(a)*1.35f,.362f,Mathf.Sin(a)*1.35f),p+new Vector3(Mathf.Cos(c)*1.35f,.362f,Mathf.Sin(c)*1.35f),.06f);}
            const int segments=64;
            for(int i=0;i<segments;i++){
                float a=i*Mathf.PI*2/segments,c=(i+1)*Mathf.PI*2/segments,mid=(a+c)*.5f;
                for(int row=0;row<7;row++){
                    float rx=9.0f+row*.47f,rz=6.6f+row*.47f,y=.64f+row*.58f;
                    Vector3 innerA=p+new Vector3(Mathf.Cos(a)*rx,y,Mathf.Sin(a)*rz),innerC=p+new Vector3(Mathf.Cos(c)*rx,y,Mathf.Sin(c)*rz);
                    Vector3 outerA=p+new Vector3(Mathf.Cos(a)*(rx+.48f),y,Mathf.Sin(a)*(rz+.48f)),outerC=p+new Vector3(Mathf.Cos(c)*(rx+.48f),y,Mathf.Sin(c)*(rz+.48f));
                    b.Quad(M("concrete"),innerA,innerC,outerC,outerA);
                    b.Quad(M("concrete"),innerC-Vector3.up*.58f,innerC,innerA,innerA-Vector3.up*.58f);
                    if(i%8==0)continue; // Radial aisles split the bowl into readable seating sections.
                    Material seat=M((i/8+row)%7==0?"seatGold":row%3==0?"seatLight":"seatBlue");
                    for(int chair=0;chair<2;chair++){
                        float theta=Mathf.Lerp(a,c,(chair+.5f)/2);Vector3 q=p+new Vector3(Mathf.Cos(theta)*(rx+.22f),y+.13f,Mathf.Sin(theta)*(rz+.22f));float rotation=-theta*Mathf.Rad2Deg+90;
                        b.Box(seat,q,new Vector3(.34f,.09f,.35f),rotation);Vector3 back=q+new Vector3(Mathf.Cos(theta)*.16f,.19f,Mathf.Sin(theta)*.16f);b.Box(seat,back,new Vector3(.34f,.36f,.07f),rotation);
                    }
                }
                // Glazed perimeter concourse, concrete columns and a continuous open roof.
                Vector3 wallA=p+new Vector3(Mathf.Cos(a)*12.9f,1.0f,Mathf.Sin(a)*10.65f),wallC=p+new Vector3(Mathf.Cos(c)*12.9f,1.0f,Mathf.Sin(c)*10.65f);
                b.Quad(M("glassDeep"),wallA,wallA+Vector3.up*4.7f,wallC+Vector3.up*4.7f,wallC);
                Vector3 roofOutA=p+new Vector3(Mathf.Cos(a)*13.65f,6.4f,Mathf.Sin(a)*11.75f),roofOutC=p+new Vector3(Mathf.Cos(c)*13.65f,6.4f,Mathf.Sin(c)*11.75f);
                Vector3 roofInA=p+new Vector3(Mathf.Cos(a)*10.25f,7.8f,Mathf.Sin(a)*7.65f),roofInC=p+new Vector3(Mathf.Cos(c)*10.25f,7.8f,Mathf.Sin(c)*7.65f);
                b.Quad(M(i%4==0?"glassSilver":"ivory"),roofInA,roofInC,roofOutC,roofOutA);
                b.Quad(M("metal"),roofOutA-Vector3.up*.24f,roofOutA,roofOutC,roofOutC-Vector3.up*.24f);
                b.Quad(M("metal"),roofInC-Vector3.up*.22f,roofInC,roofInA,roofInA-Vector3.up*.22f);
                if(i%4==0){
                    Vector3 foot=p+new Vector3(Mathf.Cos(a)*12.9f,.15f,Mathf.Sin(a)*10.65f);b.Beam(M("ivory"),foot,roofOutA,.26f);b.Beam(M("metal"),roofOutA,roofInA,.11f);b.Beam(M("metal"),roofOutA-Vector3.up*.4f,roofInA,.08f);
                }
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){
                Vector3 q=p+new Vector3(x*10.7f,.15f,z*10.7f);b.Cylinder(M("metal"),q,.13f,11.6f,12,.08f);
                b.Box(M("dark"),q+Vector3.up*11.7f,new Vector3(2.2f,.8f,.28f),x*z*35);
                b.Box(lamp,q+new Vector3(0,11.7f,-.16f),new Vector3(1.95f,.59f,.12f),x*z*35);
            }
            // Four entrance canopies and signs, ticket bollards, landscaping and forecourt furniture.
            for(int side=-1;side<=1;side+=2){
                b.Box(M("ivory"),p+new Vector3(0,3.2f,side*12.0f),new Vector3(5.2f,.20f,3.2f));
                b.Box(M("blue"),p+new Vector3(0,2.72f,side*12.75f),new Vector3(4.8f,.56f,.18f));
                for(int i=-2;i<=2;i++)Bollard(b,p+new Vector3(i*.9f,.16f,side*13.6f));
                Tree(b,p+new Vector3(-8.6f,.15f,side*12.8f),3.7f,variant+side);Tree(b,p+new Vector3(8.6f,.15f,side*12.8f),3.7f,variant+side+9);
                Bench(b,p+new Vector3(-5.0f,.16f,side*13.6f),0);Bench(b,p+new Vector3(5.0f,.16f,side*13.6f),0);
            }
            b.Box(M("dark"),p+new Vector3(0,6.3f,7.8f),new Vector3(3.6f,1.65f,.2f));b.Box(M("blue"),p+new Vector3(0,6.3f,7.66f),new Vector3(3.25f,1.35f,.05f));
            for(int i=-1;i<=1;i+=2){b.Box(lamp,p+new Vector3(i*.8f,6.3f,7.61f),new Vector3(.65f,.85f,.04f));b.Box(M("dark"),p+new Vector3(i*.8f,6.3f,7.58f),new Vector3(.37f,.53f,.04f));}
        }
    }
}
