using System.Collections.Generic;
using UnityEngine;

namespace HelloWorld
{
    // Local presentation only: reuse the actual face facets, never add a hit collider.
    internal static class DuelInkFace
    {
        public static Mesh Surface(Mesh skin)
        {
            var vertices=skin.vertices;var normals=skin.normals;var source=skin.triangles;
            var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            // Flat normals split vertices at facet edges. Share the offset there so ink has no seams.
            var offsets=new Dictionary<Vector3,Vector3>();
            for(int i=0;i<vertices.Length;i++)
            {offsets.TryGetValue(vertices[i],out var sum);offsets[vertices[i]]=sum+normals[i];}
            for(int i=0;i<vertices.Length;i++)
            {
                uv[i]=new Vector2(vertices[i].x/.36f+.5f,vertices[i].y/.44f+.5f);
                vertices[i]+=offsets[vertices[i]].normalized*.0015f;
            }
            for(int i=0;i<source.Length;i+=3)
            {
                int a=source[i],b=source[i+1],c=source[i+2];
                if(normals[a].z>=-.15f||(vertices[a].z+vertices[b].z+vertices[c].z)/3>=-.02f)continue;
                triangles.Add(a);triangles.Add(b);triangles.Add(c);
            }
            var mesh=new Mesh{name="Ink following face facets"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;
            mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }

        public static Texture2D Texture()
        {
            const int size=256;var pixels=new Color32[size*size];
            // Elliptical splashes and narrow runs leave irregular skin gaps at the edges.
            var blobs=new[]{new Vector4(.46f,.54f,.26f,.19f),new Vector4(.66f,.48f,.22f,.16f),
                new Vector4(.31f,.43f,.18f,.15f),new Vector4(.56f,.68f,.15f,.12f),
                new Vector4(.25f,.64f,.10f,.08f),new Vector4(.76f,.64f,.07f,.06f),
                new Vector4(.38f,.28f,.035f,.15f),new Vector4(.64f,.30f,.025f,.17f),
                new Vector4(.80f,.37f,.035f,.04f),new Vector4(.16f,.50f,.03f,.035f),
                new Vector4(.32f,.77f,.025f,.025f),new Vector4(.69f,.76f,.03f,.02f)};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float u=(x+.5f)/size,v=(y+.5f)/size,alpha=0;
                foreach(var b in blobs)
                {
                    float dx=(u-b.x)/b.z,dy=(v-b.y)/b.w;
                    float edge=1+.075f*Mathf.Sin(u*97+v*53)+.045f*Mathf.Sin(v*137-u*79);
                    alpha=Mathf.Max(alpha,Mathf.Clamp01((edge-Mathf.Sqrt(dx*dx+dy*dy))*32));
                }
                pixels[y*size+x]=new Color32(255,255,255,(byte)(alpha*255));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Face ink splashes and drips",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
    }
}
