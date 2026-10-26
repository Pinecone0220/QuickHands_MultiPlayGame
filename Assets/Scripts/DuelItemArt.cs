using UnityEngine;
namespace HelloWorld
{
    // Matches the existing procedural weapon icon style. No UI text is baked into the thumbnails.
    public static class DuelItemArt
    {
        public static string Name(DuelItem item)=>item==DuelItem.NoruFace?"노루페이스":item==DuelItem.Bullet?"총알":item==DuelItem.Ink?"먹물팩":item==DuelItem.Recovery?"응급 앰플":"테이저건";
        public static Texture2D Icon(DuelItem item)
            => CreateIcon(item);
        public static Texture2D CreateIcon(DuelItem item)
        {
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name=Name(item),filterMode=FilterMode.Bilinear};var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                Color c=Color.clear;
                if(item==DuelItem.NoruFace)
                {
                    bool body=x>=38&&x<=89&&y>=21&&y<=96;
                    bool sleeve=y>=27&&y<=88&&(x>=18&&x<=40||x>=87&&x<=109);
                    if(body||sleeve)c=new Color(.10f,.16f,.21f,1);
                    if((body||sleeve)&&y%14<=2)c=new Color(.045f,.075f,.10f,1);
                    if(body&&x>=41&&x<=45)c=new Color(.23f,.30f,.35f,1);
                    if(y>=91&&y<=106&&(x>=42&&x<=58||x>=69&&x<=85))c=new Color(.17f,.23f,.28f,1);
                    if(y>=22&&y<=94&&x>=62&&x<=65)c=new Color(.65f,.69f,.65f,1);
                    if(y>=24&&y<=27&&x>=20&&x<=107)c=new Color(.055f,.085f,.11f,1);
                }
                else if(item==DuelItem.Bullet)
                {
                    for(int cartridge=0;cartridge<2;cartridge++)
                    {
                        int cx=43+cartridge*40;float dx=Mathf.Abs(x-cx);
                        if(y>=24&&y<=77&&dx<=12)c=new Color(.72f,.51f,.19f,1);
                        if(y>=27&&y<=75&&x>=cx-8&&x<=cx-4)c=new Color(.96f,.79f,.42f,1);
                        if(y>=20&&y<=27&&dx<=15)c=new Color(.88f,.68f,.29f,1);
                        if(y>=77&&y<=105&&dx<=12*(1-(y-77)/30f))c=new Color(.67f,.37f,.20f,1);
                        if(y>=78&&y<=98&&x>=cx-5&&x<=cx-2&&dx<=12*(1-(y-77)/30f))c=new Color(.89f,.61f,.37f,1);
                    }
                }
                else if(item==DuelItem.Ink)
                {
                    float width=28*(1-Mathf.Abs(y-57)/85f);
                    if(y>=25&&y<=86&&Mathf.Abs(x-63)<=width)c=new Color(.19f,.12f,.27f,1);
                    if(y>=30&&y<=80&&x>=46&&x<=50)c=new Color(.42f,.30f,.53f,1);
                    if(y>=84&&y<=92&&x>=48&&x<=78)c=new Color(.8f,.67f,.34f,1);
                    if(y>=92&&y<=104&&Mathf.Abs(x-63)<(106-y)*.7f)c=new Color(.24f,.16f,.33f,1);
                    if((x-99)*(x-99)+(y-39)*(y-39)<35||(x-30)*(x-30)+(y-92)*(y-92)<20)c=new Color(.30f,.20f,.43f,1);
                }
                else if(item==DuelItem.Recovery)
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
