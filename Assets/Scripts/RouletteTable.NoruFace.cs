using UnityEngine;
namespace HelloWorld
{
    public sealed partial class RouletteTable
    {
        private Mesh noruPanelMesh;
        private readonly Material[] noruFabrics = new Material[RouletteRules.MaxPlayers];
        private readonly Transform[][] noruParts=new Transform[RouletteRules.MaxPlayers][];
        private void BuildNoruFace(Puppet p,int seat)
        {
            var fabric=Mat("NoruFace padded fabric "+seat,new Color(.09f,.16f,.21f),0,.12f);
            noruFabrics[seat]=fabric;
            var trim=Mat("NoruFace zip "+seat,new Color(.43f,.47f,.47f),.25f,.18f);
            var seam=Mat("NoruFace dark stitching "+seat,new Color(.045f,.075f,.09f),0,.12f);
            if(noruPanelMesh==null)noruPanelMesh=AvatarMesh("NoruFace rounded quilt",new Vector4(-.5f,.47f,.47f,0),new Vector4(-.40f,.54f,.54f,0),new Vector4(.40f,.54f,.54f,0),new Vector4(.5f,.47f,.47f,0));
            var body=new GameObject("NoruFace padded jacket").transform;body.SetParent(p.Torso,false);
            for(int row=0;row<5;row++)
                MeshPart("Quilted body "+row,noruPanelMesh,body,new Vector3(0,-.23f+row*.125f,-.005f),new Vector3(.66f+row*.015f,.119f,.54f),fabric);
                        Part("Front zip",PrimitiveType.Cube,body,new Vector3(0,.02f,-.282f),new Vector3(.015f,.61f,.015f),trim);
            Part("Zip pull",PrimitiveType.Cube,body,new Vector3(.018f,.14f,-.292f),new Vector3(.025f,.05f,.012f),trim);
            foreach(float side in new[]{-1f,1f})
            {
                Part("Pocket opening",PrimitiveType.Cube,body,new Vector3(side*.20f,-.20f,-.282f),new Vector3(.17f,.012f,.015f),seam);
                Part("Pocket zip",PrimitiveType.Cube,body,new Vector3(side*.20f,-.19f,-.29f),new Vector3(.11f,.006f,.008f),trim);
                Part("Hem stitching",PrimitiveType.Cube,body,new Vector3(side*.18f,-.30f,-.28f),new Vector3(.28f,.008f,.009f),seam);
            }
            foreach(float side in new[]{-1f,1f})
                Part("Raised collar",PrimitiveType.Cube,body,new Vector3(side*.11f,.40f,0),new Vector3(.18f,.13f,.32f),fabric);
            var bones=new[]{p.Upper,p.Forearm,p.LeftArm,p.LeftForearm};noruParts[seat]=new Transform[5];noruParts[seat][0]=body;
            for(int arm=0;arm<bones.Length;arm++)
            {
                var sleeve=new GameObject("NoruFace padded sleeve "+arm).transform;sleeve.SetParent(bones[arm],false);noruParts[seat][arm+1]=sleeve;
                for(int row=0;row<3;row++)
                    MeshPart("Quilted sleeve "+row,noruPanelMesh,sleeve,new Vector3(0,(row-1)*.32f,0),new Vector3(1.45f,.30f,1.45f),fabric);
            }
            ShowNoruFace(seat,false);
        }
        public GameObject CloneNoruForPreview()
        {
            var root=new GameObject("NoruFace preview");
            var body=Instantiate(noruParts[0][0].gameObject,root.transform);body.transform.localPosition=new Vector3(0,.16f,0);body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one;body.SetActive(true);
            for(int side=0;side<2;side++)
            {
                var sleeve=Instantiate(noruParts[0][side==0?1:3].gameObject,root.transform);sleeve.transform.localPosition=new Vector3(side==0?-.43f:.43f,.20f,0);sleeve.transform.localRotation=Quaternion.identity;sleeve.transform.localScale=new Vector3(.16f,.55f,.16f);sleeve.SetActive(true);
            }
            return root;
        }
        private void ShowNoruFace(int seat,bool visible)
        {if(noruParts[seat]!=null)foreach(var part in noruParts[seat])part.gameObject.SetActive(visible);}
    }
}
