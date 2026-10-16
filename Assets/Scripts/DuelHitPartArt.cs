using UnityEngine;

namespace HelloWorld
{
    // Faceted head, trousers and boots echo the table's low-poly, featureless avatars.
    public static class DuelHitPartArt
    {
        public static Texture2D Icon(DuelHitPart part)
        {
            const int size=96;
            var pixels=new Color[size*size];
            Color light=new Color(.9f,.86f,.7f), shade=new Color(.65f,.60f,.44f), dark=new Color(.43f,.40f,.31f);
            if(part==DuelHitPart.Head)
            {
                Polygon(pixels,size,shade,37,23,59,23,61,10,73,6,23,6,35,10);
                Polygon(pixels,size,light,26,74,37,84,61,84,72,73,69,40,58,26,40,26,28,40);
                Polygon(pixels,size,shade,26,74,37,65,39,39,48,26,40,26,28,40);
                Polygon(pixels,size,shade,61,84,72,73,69,40,58,26,60,59);
                Polygon(pixels,size,dark,26,74,29,84,40,90,61,89,71,80,72,67,61,73,38,74,28,63);
                Polygon(pixels,size,shade,29,84,40,90,61,89,67,84,39,82);
            }
            else if(part==DuelHitPart.Legs)
            {
                Polygon(pixels,size,light,27,88,67,88,71,72,60,46,69,19,54,15,43,44,46,64,39,48,35,19,20,19,23,50);
                Polygon(pixels,size,shade,27,88,38,82,32,49,28,19,20,19,23,50);
                Polygon(pixels,size,shade,57,81,67,88,71,72,60,46,69,19,61,17,51,46);
                Polygon(pixels,size,shade,20,22,35,22,37,12,29,7,10,7,10,13);
                Polygon(pixels,size,light,54,21,69,24,72,14,85,11,85,5,60,5,54,10);
                Polygon(pixels,size,dark,10,10,29,10,37,15,37,9,29,4,10,4);
                Polygon(pixels,size,shade,60,9,85,9,85,4,60,4,54,9,54,14);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false)
            {name=part+" kill icon",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        private static void Polygon(Color[] pixels,int size,Color color,params float[] points)
        {
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool inside=false;float px=x+.5f,py=y+.5f;
                for(int i=0,j=points.Length-2;i<points.Length;j=i,i+=2)
                    if((points[i+1]>py)!=(points[j+1]>py)&&px<(points[j]-points[i])*(py-points[i+1])/(points[j+1]-points[i+1])+points[i])inside=!inside;
                if(inside)pixels[y*size+x]=color;
            }
        }
    }
}
