using System.Collections.Generic;
using UnityEngine;

namespace HelloWorld
{
    // Presentation geometry only. Every triangle owns its vertices for flat face normals.
    internal static class DuelFacetedMesh
    {
        // A section contains height, X radius, Z radius and Z offset, in local space.
        public static Mesh Sections(string name, params Vector4[] sections)
        {
            const int sides=8;
            var points=new List<Vector3>();var indices=new List<int>();
            Vector3 Point(int ring,int side)
            {
                var s=sections[ring];float angle=(side+.5f)*Mathf.PI*2/sides;
                return new Vector3(Mathf.Sin(angle)*s.y,s.x,Mathf.Cos(angle)*s.z+s.w);
            }
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int i=points.Count;points.Add(a);points.Add(b);points.Add(c);indices.Add(i);indices.Add(i+1);indices.Add(i+2);}
            for(int ring=0;ring<sections.Length-1;ring++)for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                var a=Point(ring,side);var b=Point(ring,next);var c=Point(ring+1,next);var d=Point(ring+1,side);
                Triangle(a,b,d);Triangle(b,c,d);
            }
            int last=sections.Length-1;
            for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                Triangle(new Vector3(0,sections[0].x,sections[0].w),Point(0,next),Point(0,side));
                Triangle(new Vector3(0,sections[last].x,sections[last].w),Point(last,side),Point(last,next));
            }
            var mesh=new Mesh{name=name};mesh.SetVertices(points);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
