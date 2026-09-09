using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seabright
{
    public sealed partial class CityView
    {
        sealed class Walker {
            public List<Vector2Int> Route;
            public int Segment, Outfit;
            public float Progress, Pace, Phase, Side;
            public Vector3 Position;
        }
        readonly List<Walker> walkers=new List<Walker>();
        readonly List<Material> outfits=new List<Material>();
        readonly List<List<Matrix4x4>> outfitMatrices=new List<List<Matrix4x4>>();
        readonly List<Matrix4x4> headMatrices=new List<Matrix4x4>(), legMatrices=new List<Matrix4x4>();
        Mesh personPart;
        Material skin, trousers;
        float walkingTime;
        public int PedestrianCount=>walkers.Count;
        public Vector3 PedestrianMotionSample=>walkers.Count>0?walkers[0].Position:Vector3.zero;

        void BuildPedestrians() {
            personPart=RoundedPersonPart();
            foreach(string hex in new[]{"D99D50","598EB9","C15F54","D1C6B3","548979","796E9D"}) {
                outfits.Add(M("Pedestrian coat "+hex,C(hex)));outfitMatrices.Add(new List<Matrix4x4>());
            }
            skin=M("Pedestrian skin",C("B98B6A"));trousers=M("Pedestrian trousers",C("344453"));
        }
        void RefreshPedestrianRoutes() {
            walkers.Clear();if(roadTiles.Count<4)return;
            var local=new List<Vector2Int>();
            foreach(var road in roadTiles) {
                bool lived=false;
                for(int dz=-2;dz<=2&&!lived;dz++)for(int dx=-2;dx<=2&&!lived;dx++) {
                    var t=sim.Get(road.x+dx,road.y+dz);
                    lived=t!=null&&t.Kind!=TileKind.Road&&t.Kind!=TileKind.Empty&&t.Level>0&&(t.Residents>0||t.Jobs>0||t.Kind==TileKind.Park||t.Kind==TileKind.Stadium);
                }
                if(lived)local.Add(road);
            }
            if(local.Count<3)return;
            int count=Mathf.Clamp(sim.Population/7,6,130);
            for(int i=0;i<count;i++)for(int attempt=0;attempt<12;attempt++) {
                var from=local[(i*13+attempt*7)%local.Count];var to=local[(i*37+attempt*19+3)%local.Count];
                if((from-to).sqrMagnitude<4)continue;
                var path=sim.FindRoadPath(from,to);if(path==null||path.Count<3)continue;
                walkers.Add(new Walker{Route=path,Segment=Mathf.Min(path.Count-2,(int)(H(i,77)*(path.Count-1))),Progress=H(i,88)*10,Pace=1.55f+H(i,93)*.7f,Phase=H(i,97)*6.28f,Outfit=i%outfits.Count,Side=i%2==0?1:-1});break;
            }
        }
        void UpdatePedestrians(float dt) {
            if(personPart==null)return;
            walkingTime+=Mathf.Max(0,dt);headMatrices.Clear();legMatrices.Clear();foreach(var list in outfitMatrices)list.Clear();
            foreach(var w in walkers) {
                w.Progress+=Mathf.Max(0,dt)*w.Pace;
                while(w.Progress>=10){w.Progress-=10;w.Segment++;if(w.Segment>=w.Route.Count-1){w.Route.Reverse();w.Segment=0;w.Side=-w.Side;}}
                var ta=w.Route[w.Segment];var tb=w.Route[w.Segment+1];
                var a=CitySimulation.World(ta.x,ta.y);var b=CitySimulation.World(tb.x,tb.y);var dir=(b-a).normalized;
                var side=Vector3.Cross(Vector3.up,dir)*4.15f*w.Side;
                Vector3 p=Vector3.Lerp(a,b,w.Progress/10)+side+Vector3.up*.175f;
                w.Position=p;var facing=Quaternion.LookRotation(dir);
                float stride=Mathf.Sin(walkingTime*w.Pace*4+w.Phase)*27;
                var root=Matrix4x4.TRS(p,facing,Vector3.one);
                outfitMatrices[w.Outfit].Add(root*Matrix4x4.TRS(new Vector3(0,1.1f,0),Quaternion.identity,new Vector3(.48f,.68f,.30f)));
                headMatrices.Add(root*Matrix4x4.TRS(new Vector3(0,1.68f,0),Quaternion.identity,new Vector3(.30f,.34f,.31f)));
                for(int sign=-1;sign<=1;sign+=2) {
                    var leg=root*Matrix4x4.TRS(new Vector3(sign*.13f,.79f,0),Quaternion.Euler(stride*sign,0,0),Vector3.one);
                    legMatrices.Add(leg*Matrix4x4.TRS(new Vector3(0,-.36f,0),Quaternion.identity,new Vector3(.17f,.76f,.18f)));
                    var arm=root*Matrix4x4.TRS(new Vector3(sign*.30f,1.35f,0),Quaternion.Euler(-stride*sign,0,sign*7),Vector3.one);
                    outfitMatrices[w.Outfit].Add(arm*Matrix4x4.TRS(new Vector3(0,-.25f,0),Quaternion.identity,new Vector3(.14f,.57f,.16f)));
                }
            }
            for(int i=0;i<outfits.Count;i++)DrawPeople(outfits[i],outfitMatrices[i]);
            DrawPeople(skin,headMatrices);DrawPeople(trousers,legMatrices);
        }
        void DrawPeople(Material mat,List<Matrix4x4> instances) {
            if(instances.Count>0)Graphics.DrawMeshInstanced(personPart,0,mat,instances,null,ShadowCastingMode.On,true,9,null,LightProbeUsage.Off);
        }
        static Mesh RoundedPersonPart() {
            // Smooth normals and rounded silhouettes remain readable when zoomed to street scale.
            const int rings=8,sides=10;var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++) {
                float lat=Mathf.PI*y/rings,lon=2*Mathf.PI*x/sides;
                var n=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));
                normals.Add(n);vertices.Add(n*.5f);
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++) {
                int a=y*(sides+1)+x,b=a+sides+1;
                triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
            }
            var mesh=new Mesh{name="Rounded pedestrian geometry"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
    }
}
