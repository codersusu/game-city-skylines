using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seabright
{
    public sealed partial class CityView
    {
        sealed class Car
        {
            public List<Vector2Int> route;
            public int segment, kind;
            public float progress, speed;
            public Vector3 position;
            public Quaternion rotation=Quaternion.identity;
        }
        readonly List<Vector2Int> roadTiles=new List<Vector2Int>();
        readonly List<Car> traffic=new List<Car>();
        readonly List<Mesh> carMeshes=new List<Mesh>();
        readonly List<Material> carMaterials=new List<Material>();
        readonly List<List<Matrix4x4>> carMatrices=new List<List<Matrix4x4>>();
        int roadHash, trafficRevision=-1;
        bool trafficReady;
        public int TrafficCount => traffic.Count;
        public Vector3 TrafficMotionSample => traffic.Count>0?traffic[0].position:Vector3.zero;

        void BuildTraffic()
        {
            string[] names={"sedan","suv","taxi","delivery","van","truck","ambulance"};
            foreach(string name in names) {
                var model=LoadModel("Kenney/cars/"+name); if(model==null)continue;
                var geometry=new MeshBatches.Geometry();
                float length=name=="truck"?5.6f:name=="delivery"?4.8f:4.1f;
                float scale=length/Mathf.Max(model.bounds.size.x,model.bounds.size.z);
                var norm=Matrix4x4.Scale(Vector3.one*scale)*Matrix4x4.Translate(new Vector3(-model.bounds.center.x,-model.bounds.min.y,-model.bounds.center.z));
                for(int i=0;i<model.meshes.Count;i++)for(int j=0;j<model.meshes[i].subMeshCount;j++)geometry.Append(model.meshes[i],norm*model.matrices[i],j);
                carMeshes.Add(geometry.ToMesh("Traffic "+name));
                model.material.enableInstancing=true;
                carMaterials.Add(model.material);carMatrices.Add(new List<Matrix4x4>());
            }
            BuildPedestrians(); trafficReady=true; RefreshTrafficPaths(true);
        }
        void RefreshTrafficPaths(bool force=false)
        {
            if(!trafficReady)return;
            int hash=1;unchecked {
                foreach(var t in sim.Tiles)if(t.Kind==TileKind.Road&&t.Connected)hash=hash*31+t.X*48+t.Z;
                hash=hash*31+sim.Population/100;
            }
            if(!force&&hash==roadHash)return;
            roadHash=hash;roadTiles.Clear();
            foreach(var t in sim.Tiles)if(t.Kind==TileKind.Road&&t.Connected)roadTiles.Add(new Vector2Int(t.X,t.Z));
            traffic.Clear();
            if(roadTiles.Count>=4&&carMeshes.Count>0) {
                int count=Mathf.Clamp(8+sim.Population/18,8,130);
                for(int i=0;i<count;i++) {
                    var car=new Car{kind=i%carMeshes.Count,speed=8.5f+H(i,11)*4.5f};
                    if(!ChooseRoute(car,i*17))continue;
                    car.segment=Mathf.Min(car.route.Count-2,Mathf.FloorToInt(H(i,24)*(car.route.Count-1)));
                    car.progress=H(i,18)*CitySimulation.CellSize;
                    var a=CitySimulation.World(car.route[car.segment].x,car.route[car.segment].y);
                    var b=CitySimulation.World(car.route[car.segment+1].x,car.route[car.segment+1].y);
                    car.rotation=Quaternion.LookRotation(b-a);car.position=Vector3.Lerp(a,b,car.progress/10);
                    traffic.Add(car);
                }
            }
            RefreshPedestrianRoutes();
        }
        bool ChooseRoute(Car car,int seed)
        {
            for(int attempt=0;attempt<20;attempt++) {
                int ai=(seed*31+attempt*17)&int.MaxValue, bi=(seed*73+attempt*43+73)&int.MaxValue;
                var a=roadTiles[ai%roadTiles.Count];var b=roadTiles[bi%roadTiles.Count];
                if((a-b).sqrMagnitude<9)continue;
                var path=sim.FindRoadPath(a,b);if(path==null||path.Count<4)continue;
                car.route=path;car.segment=0;car.progress=0;return true;
            }
            return false;
        }
        public void UpdateTraffic(float dt)
        {
            UpdateSteam(dt);if(!trafficReady)return;
            if(trafficRevision!=sim.Revision){trafficRevision=sim.Revision;RefreshTrafficPaths();}
            foreach(var matrices in carMatrices)matrices.Clear();
            foreach(var car in traffic) {
                if(car.route==null||car.route.Count<2)continue;
                var current=car.route[car.segment];
                car.progress+=Mathf.Max(0,dt)*car.speed*Mathf.Lerp(1,.55f,sim.RoadTraffic(current.x,current.y));
                while(car.progress>=10) { car.progress-=10;car.segment++;if(car.segment>=car.route.Count-1){car.route.Reverse();car.segment=0;} }
                Vector3 a=CitySimulation.World(car.route[car.segment].x,car.route[car.segment].y);
                Vector3 b=CitySimulation.World(car.route[car.segment+1].x,car.route[car.segment+1].y);
                Vector3 direction=(b-a).normalized;
                Vector3 target=Vector3.Lerp(a,b,car.progress/10)+Vector3.Cross(Vector3.up,direction)*1.65f+Vector3.up*.235f;
                car.position=target;
                car.rotation=Quaternion.Slerp(car.rotation,Quaternion.LookRotation(direction),dt>0?Mathf.Min(1,dt*9):1);
                carMatrices[car.kind].Add(Matrix4x4.TRS(target,car.rotation*Quaternion.Euler(0,180,0),Vector3.one));
            }
            // Dynamic draw variants are explicitly retained in GraphicsSettings. Layer 8
            // also permits a real render readback in the standalone visibility check.
            for(int i=0;i<carMeshes.Count;i++)if(carMatrices[i].Count>0)
                Graphics.DrawMeshInstanced(carMeshes[i],0,carMaterials[i],carMatrices[i],null,ShadowCastingMode.On,true,8,null,LightProbeUsage.Off);
            UpdatePedestrians(dt);
        }
    }
}
