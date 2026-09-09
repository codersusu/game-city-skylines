using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Seabright
{
    // Geometry is collected by shared material, keeping the city to a few dozen draws.
    internal sealed class MeshBatches
    {
        internal sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int k = vertices.Count; Vector3 n = Vector3.Cross(b-a,c-a).normalized;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for(int i=0;i<4;i++) normals.Add(n);
                uvs.Add(Vector2.zero);uvs.Add(Vector2.right);uvs.Add(Vector2.one);uvs.Add(Vector2.up);
                triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);triangles.Add(k);triangles.Add(k+2);triangles.Add(k+3);
            }
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {
                int k=vertices.Count;Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);for(int i=0;i<3;i++){normals.Add(n);uvs.Add(Vector2.zero);triangles.Add(k+i);}
            }
            public void Append(Mesh mesh, Matrix4x4 matrix, int submesh)
            {
                int k=vertices.Count;var vs=mesh.vertices;var ns=mesh.normals;var uv=mesh.uv;
                Matrix4x4 normal=matrix.inverse.transpose;
                for(int i=0;i<vs.Length;i++){vertices.Add(matrix.MultiplyPoint3x4(vs[i]));normals.Add(ns.Length>i?normal.MultiplyVector(ns[i]).normalized:Vector3.up);uvs.Add(uv.Length>i?uv[i]:Vector2.zero);}
                foreach(int index in mesh.GetTriangles(submesh)) triangles.Add(k+index);
            }
            public Mesh ToMesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
            }
        }
        private readonly Dictionary<Material,Geometry> batches=new Dictionary<Material,Geometry>();
        public Geometry For(Material material){if(!batches.TryGetValue(material,out var g)){g=new Geometry();batches.Add(material,g);}return g;}
        public void Quad(Material m,Vector3 a,Vector3 b,Vector3 c,Vector3 d){For(m).Quad(a,b,c,d);}
        public void Box(Material m,Vector3 p,Vector3 size,float yaw=0)
        {
            Quaternion r=Quaternion.Euler(0,yaw,0);Vector3 h=size*.5f;
            Vector3 a=p+r*new Vector3(-h.x,-h.y,-h.z),b=p+r*new Vector3(h.x,-h.y,-h.z),c=p+r*new Vector3(h.x,-h.y,h.z),d=p+r*new Vector3(-h.x,-h.y,h.z);
            Vector3 e=a+Vector3.up*size.y,f=b+Vector3.up*size.y,g=c+Vector3.up*size.y,j=d+Vector3.up*size.y;var q=For(m);
            q.Quad(a,e,f,b);q.Quad(b,f,g,c);q.Quad(c,g,j,d);q.Quad(d,j,e,a);q.Quad(e,j,g,f);q.Quad(a,b,c,d);
        }
        public void Cylinder(Material m,Vector3 p,float radius,float height,int sides=10,float topRadius=-1)
        {
            if(topRadius<0)topRadius=radius;var q=For(m);Vector3 top=p+Vector3.up*height;
            for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;Vector3 u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));q.Quad(p+u*radius,top+u*topRadius,top+v*topRadius,p+v*radius);q.Triangle(top,top+v*topRadius,top+u*topRadius);}
        }
        public void Beam(Material m,Vector3 a,Vector3 b,float width)
        {
            Vector3 dir=(b-a).normalized;Vector3 side=Vector3.Cross(dir,Vector3.up).normalized*width*.5f;if(side.sqrMagnitude<.00001f)side=Vector3.right*width*.5f;
            Vector3 up=Vector3.Cross(side,dir).normalized*width*.5f;var q=For(m);q.Quad(a-side-up,b-side-up,b+side-up,a+side-up);q.Quad(a+side+up,b+side+up,b-side+up,a-side+up);q.Quad(a-side+up,b-side+up,b-side-up,a-side-up);q.Quad(a+side-up,b+side-up,b+side+up,a+side+up);
        }
        public GameObject Build(string name,Transform parent)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            foreach(var pair in batches){if(pair.Value.vertices.Count==0)continue;var go=new GameObject(pair.Key.name);go.transform.SetParent(root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=pair.Value.ToMesh(name+" "+pair.Key.name);var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=pair.Key.shader.name=="Seabright/CoastalGround"?ShadowCastingMode.Off:ShadowCastingMode.On;renderer.receiveShadows=true;}
            return root;
        }
        public static void Dispose(GameObject root)
        {
            if(!root)return;foreach(var f in root.GetComponentsInChildren<MeshFilter>()){if(f.sharedMesh)Object.Destroy(f.sharedMesh);}Object.Destroy(root);
        }
    }
}
