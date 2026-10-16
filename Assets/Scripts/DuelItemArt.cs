using UnityEngine;
namespace HelloWorld
{
    // Matches the existing procedural weapon icon style. No UI text is baked into the thumbnails.
    public static class DuelItemArt
    {
        public static string Name(DuelItem item)=>item==DuelItem.Recovery?"응급 앰플":"테이저건";
        public static Texture2D Icon(DuelItem item)
            => CreateIcon(item);
        public static Texture2D CreateIcon(DuelItem item)
        {
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name=Name(item),filterMode=FilterMode.Bilinear};var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                Color c=Color.clear;
                if(item==DuelItem.Recovery)
                {
                    float aura=Mathf.Exp(-Mathf.Pow((Mathf.Sqrt(Mathf.Pow((x-64)*.85f,2)+Mathf.Pow(y-62,2))-43)/7,2));
                    c=new Color(.2f,1,.52f,aura*.32f);
                    if(x>=47&&x<=81&&y>=20&&y<=91)c=new Color(.42f,.69f,.58f,1);
                    if(x>=50&&x<=78&&y>=23&&y<=75)c=new Color(.1f,.84f,.4f,1);
                    if(x>=50&&x<=78&&y>=92&&y<=102)c=new Color(.82f,.8f,.65f,1);
                    if(x>=52&&x<=56&&y>=28&&y<=86)c=new Color(.8f,1,.87f,.9f);
                    if(x>=60&&x<=68&&y>=42&&y<=65||x>=53&&x<=75&&y>=49&&y<=57)c=new Color(.96f,1,.88f,1);
                }
                else
                {
                    bool slide=x>=34&&x<=105&&y>=72&&y<=95;
                    bool grip=y>=24&&y<=73&&x>=28+(y-24)*.27f&&x<=49+(y-24)*.27f;
                    float guard=Mathf.Pow((x-64)/17f,2)+Mathf.Pow((y-64)/13f,2);
                    if(slide||grip||guard<=1&&guard>.65f)c=new Color(.86f,.76f,.26f,1);
                    if(grip&&x>36+(y-24)*.27f)c=new Color(.09f,.13f,.15f,1);
                    if(x>=80&&x<=104&&y>=76&&y<=91)c=new Color(.12f,.19f,.21f,1);
                    if(x>=105&&x<=112&&(y>=77&&y<=81||y>=88&&y<=92))c=new Color(.72f,.94f,1,1);
                    if(y>=77&&y<=98&&x>=54&&x<=67&&Mathf.Abs(x-(61+Mathf.Sin((y-77)*.4f)*5))<2.3f)c=new Color(.95f,1,1,1);
                }
                p[y*n+x]=c;
            }
            t.SetPixels(p);t.Apply();return t;
        }
        public static Texture2D Effect(bool electric)
        {
            const int n=256;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name=electric?"Electrical interference":"Healing aurora",wrapMode=TextureWrapMode.Clamp};var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float edge=1-Mathf.Min(Mathf.Min(x,n-1-x),Mathf.Min(y,n-1-y))/55f;edge=Mathf.Clamp01(edge);
                float wave=.55f+.45f*Mathf.Sin(x*.07f+y*.05f);
                float bolt=electric&&Mathf.Abs(x-(25+Mathf.Sin(y*.17f)*8))<2||electric&&Mathf.Abs(x-(n-25+Mathf.Sin(y*.13f)*9))<2?1:0;
                p[y*n+x]=electric?new Color(.25f,.76f,1,edge*.12f+bolt*.6f):new Color(.17f,1,.43f,edge*edge*wave*.38f);
            }
            t.SetPixels(p);t.Apply(false,true);return t;
        }
    }
}
