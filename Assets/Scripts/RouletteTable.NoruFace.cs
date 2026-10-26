using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteTable
    {
        private readonly Transform[][] noruParts=new Transform[RouletteRules.MaxPlayers][];
        private void BuildNoruFace(Puppet p,int seat)
        {
            var fabric=Mat("NoruFace padded fabric "+seat,new Color(.09f,.16f,.21f),0,.12f);
            var trim=Mat("NoruFace zip "+seat,new Color(.43f,.47f,.47f),.25f,.18f);
            var body=new GameObject("NoruFace padded jacket").transform;body.SetParent(p.Torso,false);
            for(int row=0;row<5;row++)
                Part("Quilted body "+row,PrimitiveType.Cube,body,new Vector3(0,-.23f+row*.125f,-.005f),new Vector3(.66f+row*.015f,.119f,.54f),fabric);
            Part("Front zip",PrimitiveType.Cube,body,new Vector3(0,.02f,-.282f),new Vector3(.015f,.61f,.015f),trim);
            foreach(float side in new[]{-1f,1f})
                Part("Raised collar",PrimitiveType.Cube,body,new Vector3(side*.11f,.40f,0),new Vector3(.18f,.13f,.32f),fabric);
            var bones=new[]{p.Upper,p.Forearm,p.LeftArm,p.LeftForearm};noruParts[seat]=new Transform[5];noruParts[seat][0]=body;
            for(int arm=0;arm<bones.Length;arm++)
            {
                var sleeve=new GameObject("NoruFace padded sleeve "+arm).transform;sleeve.SetParent(bones[arm],false);noruParts[seat][arm+1]=sleeve;
                for(int row=0;row<3;row++)
                    Part("Quilted sleeve "+row,PrimitiveType.Cube,sleeve,new Vector3(0,(row-1)*.32f,0),new Vector3(1.45f,.30f,1.45f),fabric);
            }
            ShowNoruFace(seat,false);
        }
        private void ShowNoruFace(int seat,bool visible)
        {if(noruParts[seat]!=null)foreach(var part in noruParts[seat])part.gameObject.SetActive(visible);}
    }
}
