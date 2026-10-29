using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HelloWorld
{
    // Local presentation only. Both peers derive the same shot from the authoritative snapshot.
    // No replicated visual transform can reveal a hidden cartridge.
    public sealed partial class RouletteTable : MonoBehaviour
    {
        private Transform world, gun, barrelPivot, muzzle, hammer, barrelLatch;
        private Camera viewCamera;
        private DuelFirstPerson firstPerson;
        private DuelShotVisuals shotVisuals;
        private DuelWeaponAudio weaponAudio;
        private DuelDrinkVisuals drinkVisuals;
        private Vector3 lastEye;
        private Quaternion lastEyeRotation;
        private Light muzzleLight;
        private GameObject flash;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Mesh sleeveMesh, handMesh, gripMesh, shoeMesh;
        private readonly Material[] skinMaterials=new Material[RouletteRules.MaxPlayers];
        private readonly Vector3 cameraHome = new Vector3(0, 3.5f, -7.6f);
        private static readonly Vector3 spectatorHome = new Vector3(-4.8f, 6f, -7f);
        private const float SeatRadius = DuelGunplay.SeatRadius;
        private Quaternion cameraRotation;
        private readonly Puppet[] puppets = new Puppet[RouletteRules.MaxPlayers];
        private readonly Transform[] chairs = new Transform[RouletteRules.MaxPlayers];
        private Transform holdingHand, loadingHand;
        private Transform[] cartridges = new Transform[1];
        private Material liveTip, shellRed;
        private readonly LineRenderer[] healRings=new LineRenderer[RouletteRules.MaxPlayers];
        private readonly LineRenderer[,] rareHealRings=new LineRenderer[RouletteRules.MaxPlayers,3];
        private readonly LineRenderer[,] stunArcs=new LineRenderer[RouletteRules.MaxPlayers,3];
        public float HitPulse { get; private set; }
        public bool TryGetNameViewport(int seat, out Vector3 point)
        {
            point = default;
            if (viewCamera == null || seat < 0 || seat >= puppets.Length) return false;
            var p = puppets[seat];
            if (p == null || !p.Root.gameObject.activeInHierarchy || !p.HeadRenderers[0].enabled || p.HeadRenderers[0].forceRenderingOff) return false;
            point = viewCamera.WorldToViewportPoint(p.Head.position + Vector3.up * 0.38f);
            return point.z > viewCamera.nearClipPlane && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
        public bool TryGetChairViewport(int seat, out Vector3 point)
        {
            point = default;
            if (viewCamera == null || seat < 0 || seat >= chairs.Length || chairs[seat] == null ||
                !chairs[seat].gameObject.activeInHierarchy) return false;
            point = viewCamera.WorldToViewportPoint(chairs[seat].position + Vector3.up * 0.85f);
            return point.z > viewCamera.nearClipPlane && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1;
        }
        private const float FallDuration = 0.95f;
        private const float DeathFaceHold = 1f;
        internal const float SpectatorLookDelay=FallDuration+DeathFaceHold+.4f;
        internal static float SpectatorTransitionStart(int lives)=>lives==0?DuelLifeFeedback.PermanentRecoveryDuration:FallDuration+DeathFaceHold;
        private sealed class Puppet
        {
            public Transform Root, Body, Head, Torso, Upper, Forearm, Hand, LeftArm, LeftForearm, LeftHand;
            public Renderer[] HeadRenderers,Renderers,ArmRenderers;
            public DuelHeadLook Look;
            public int Seat;
            public bool PoseInitialized,HandsInitialized;
            public DuelCharacterPose Pose;
            public Vector3 RightPosition,LeftPosition;
            public Transform[] RightFingers,LeftFingers;
        }
        private void Awake()
        {
            firstPerson = GetComponent<DuelFirstPerson>();
            shotVisuals=gameObject.AddComponent<DuelShotVisuals>();
            weaponAudio=gameObject.AddComponent<DuelWeaponAudio>();
            gameObject.AddComponent<DuelInkVisuals>();
            gameObject.AddComponent<DuelInkAudio>();
            gameObject.AddComponent<DuelRareItemPresentation>();
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            var plane = GameObject.Find("Plane");
            if (plane != null) plane.SetActive(false);
            world = new GameObject("Single-shot room - local presentation").transform;
            world.SetParent(transform, false);
            drinkVisuals=gameObject.AddComponent<DuelDrinkVisuals>();drinkVisuals.Initialize(world);
            viewCamera = Camera.main;
            if (viewCamera != null)
            {
                viewCamera.transform.position = cameraHome;
                viewCamera.transform.LookAt(new Vector3(0, 1.1f, 0.15f));
                cameraRotation = viewCamera.transform.rotation;
                viewCamera.fieldOfView = 48;
                viewCamera.nearClipPlane = 0.05f;
                viewCamera.clearFlags = CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = new Color(0.012f, 0.016f, 0.019f);
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.23f, 0.225f, 0.21f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.025f, 0.03f, 0.033f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.035f;

            var wood = Mat("Dark walnut", new Color(0.18f, 0.095f, 0.052f), 0, 0.28f);
            var edge = Mat("Worn wood edges", new Color(0.3f, 0.19f, 0.10f), 0, 0.3f);
            var felt = Mat("Green baize", new Color(0.035f, 0.115f, 0.087f), 0, 0.08f);
            var wall = Mat("Concrete", new Color(0.11f, 0.125f, 0.13f), 0, 0.12f);
            var black = Mat("Blackened steel", new Color(0.032f, 0.043f, 0.051f), 0.75f, 0.5f);
            var steel = Mat("Machined steel", new Color(0.31f, 0.36f, 0.39f), 0.85f, 0.7f);
            var brass = Mat("Brass", new Color(0.57f, 0.37f, 0.12f), 0.72f, 0.6f);

            // All six rotated gameplay/observer cameras fit inside the room.
            const float roomEdge = 10f;
            Part("Floor", PrimitiveType.Cube, world, new Vector3(0, -0.15f, 0), new Vector3(22, 0.2f, 22), wall);
            for (int side = 0; side < 4; side++)
            {
                Quaternion facing = Quaternion.Euler(0, side * 90, 0);
                var roomWall = Part("Room wall " + side, PrimitiveType.Cube, world,
                    facing * new Vector3(0, 3, roomEdge), new Vector3(20.2f, 7, 0.15f), wall);
                roomWall.localRotation = facing;
                for (int i = -18; i <= 18; i++)
                {
                    var timber = Part("Wall timber", PrimitiveType.Cube, world,
                        facing * new Vector3(i * 0.5f, 1.3f, roomEdge - 0.15f), new Vector3(0.06f, 2.6f, 0.1f), wood);
                    timber.localRotation = facing;
                }
            }
            // Unity's cylinder is two units tall; Y scale is half the desired thickness.
            Part("Table", PrimitiveType.Cylinder, world, new Vector3(0, 0.78f, 0), new Vector3(7.5f, 0.09f, 7.5f), wood);
            Part("Felt inset", PrimitiveType.Cylinder, world, new Vector3(0, 0.878f, 0), new Vector3(7.1f, 0.009f, 7.1f), felt);
            Ring("Inlay", world, new Vector3(0, 0.884f, 0), 3.58f, 0.008f, brass, Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                Vector3 leg = SeatPosition(i).normalized * 2.8f + Vector3.up * 0.35f;
                Part("Table leg", PrimitiveType.Cylinder, world, leg, new Vector3(0.15f, 0.4f, 0.15f), wood);
            }
            Ring("Wood rim", world, new Vector3(0, 0.876f, 0), 3.68f, 0.018f, edge, Quaternion.Euler(90, 0, 0));
            Ring("Table medallion", world, new Vector3(0, 0.895f, 0), 0.47f, 0.003f, brass, Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < 6; i++)
            {
                var prop = Part("Spent casing decoration", PrimitiveType.Cylinder, world,
                    new Vector3(0.85f + (i % 3) * 0.09f, 0.925f, -0.6f + (i / 3) * 0.12f), new Vector3(0.044f, 0.045f, 0.044f), brass);
                prop.localRotation = Quaternion.Euler(90, i * 37, 0);
            }

            sleeveMesh=AvatarMesh("Faceted sleeve",new Vector4(-1,.32f,.32f,0),new Vector4(-.65f,.5f,.5f,0),new Vector4(.55f,.47f,.47f,0),new Vector4(1,.34f,.34f,0));
            handMesh=AvatarMesh("Faceted palm",new Vector4(-.5f,.36f,.37f,0),new Vector4(-.24f,.55f,.52f,0),new Vector4(.24f,.52f,.51f,0),new Vector4(.5f,.34f,.34f,0));
            gripMesh=AvatarMesh("Faceted grip fist",new Vector4(-.5f,.30f,.31f,0),new Vector4(-.23f,.49f,.48f,0),new Vector4(.25f,.52f,.50f,0),new Vector4(.5f,.34f,.36f,0));
            shoeMesh=AvatarMesh("Faceted leather shoe",new Vector4(-.5f,.43f,.48f,0),new Vector4(-.28f,.53f,.55f,0),new Vector4(.28f,.47f,.46f,.05f),new Vector4(.5f,.33f,.34f,.12f));
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            {
                var chair = new GameObject("Chair " + (i + 1)).transform;
                chair.SetParent(world, false); chair.localPosition = SeatPosition(i); chair.localRotation = SeatRotation(i);
                Part("Seat cushion", PrimitiveType.Cube, chair, new Vector3(0,.66f-DuelGunplay.SeatedDrop,0), new Vector3(.8f,.12f,.7f), wood);
                Part("Backrest", PrimitiveType.Cube, chair, new Vector3(0,1.17f-DuelGunplay.SeatedDrop,.33f), new Vector3(.76f,.96f,.14f), wood);
                Part("Back inset", PrimitiveType.Cube, chair, new Vector3(0,1.2f-DuelGunplay.SeatedDrop,.247f), new Vector3(.62f,.7f,.025f), felt);
                foreach (float x in new[]{-.3f,.3f}) foreach (float z in new[]{-.27f,.27f})
                    Part("Chair leg", PrimitiveType.Cube, chair, new Vector3(x,(.6f-DuelGunplay.SeatedDrop)/2,z), new Vector3(.09f,.6f-DuelGunplay.SeatedDrop,.09f), wood);
                foreach (float x in new[]{-.3f,.3f})
                    Part("Chair brace", PrimitiveType.Cube, chair, new Vector3(x,.16f,0), new Vector3(.07f,.07f,.58f), wood);
                chairs[i] = chair;
                puppets[i] = BuildPuppet(i, SeatPosition(i), SeatRotation(i));
            }
            liveTip = Mat("Copper live bullet tip", new Color(0.8f, 0.25f, 0.07f), 0.45f, 0.6f);
            shellRed = Mat("Red shotgun shell", new Color(0.65f, 0.08f, 0.045f), 0, 0.35f);
            Part("Hanging cable", PrimitiveType.Cylinder, world, new Vector3(0, 4.3f, 0.15f), new Vector3(0.018f, 0.5f, 0.018f), black);
            Part("Lamp shade", PrimitiveType.Cylinder, world, new Vector3(0, 3.83f, 0.15f), new Vector3(0.9f, 0.055f, 0.9f), black);
            LightAt("Warm overhead", new Vector3(0, 3.75f, 0), new Color(1, 0.80f, 0.59f), 10, 9, true);
            LightAt("Neutral table fill", new Vector3(0, 2.65f, 0), new Color(.88f, .85f, .80f), 8, 10, false);

            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
            for (int weapon = 0; weapon < WeaponRules.Count; weapon++)
            {
                cartridges = new Transform[WeaponRules.Capacity((DuelWeapon)weapon)];
                BuildSingleShot((DuelWeapon)weapon, black, steel, wood, brass);
                gun.name = WeaponRules.Name((DuelWeapon)weapon) + " seat " + i;
                bool rifle=(DuelWeapon)weapon==DuelWeapon.BAS;
                holdingHand = rifle?BuildBasGrip(skinMaterials[i]):AvatarHand("Grip hand", gun, new Vector3(0, -0.26f, -0.24f), new Vector3(0.22f, 0.18f, 0.25f), skinMaterials[i], true);
                loadingHand = rifle?BuildBasPinch(skinMaterials[i]):AvatarHand("Loading hand", world, Vector3.zero, new Vector3(0.13f, 0.1f, 0.16f), skinMaterials[i], false, true);
                rigs[i, weapon] = new GunRig { Gun = gun, Barrel = barrelPivot, Muzzle = muzzle, Holding = holdingHand,
                    Loading = loadingHand, Hammer = hammer, Latch = barrelLatch, Flash = flash, Light = muzzleLight, Cartridges = cartridges,
                    EjectedCase=rifle?BuildBasCase(brass,black):null,Support=rifle?BuildBasSupport(skinMaterials[i]):null };
                gun.localPosition = Rest(i); gun.localRotation = RestRotation(i);
                gun.gameObject.SetActive(false); loadingHand.gameObject.SetActive(false);
            }
        }
        private sealed class GunRig
        {
            public Transform Gun, Barrel, Muzzle, Holding, Loading, Hammer, Latch;
            public GameObject Flash; public Light Light;
            public Transform[] Cartridges;
            public Transform EjectedCase,Support;
            public bool ViewPoseInitialized;
            public Vector3 ViewOffset;
            public Quaternion ViewRotation;
            public bool BasSeen;
            public int BasRound,BasLife,BasSession,BasSpent,BasShots;
            public double CaseAt=-1;
            public Vector3 CaseOrigin;
            public Quaternion CaseRotation;
        }
        private readonly int[] cosmeticCache = { -1, -1, -1, -1, -1, -1 };
        private readonly GunRig[,] rigs = new GunRig[RouletteRules.MaxPlayers, WeaponRules.Count];
        public bool TryVisibleMuzzle(int seat,DuelWeapon weapon,out Vector3 position,out Vector3 direction)
        {
            position=direction=Vector3.zero;
            if(seat<0||seat>=RouletteRules.MaxPlayers||!WeaponRules.IsValid(weapon))return false;
            var rig=rigs[seat,(int)weapon];
            if(rig==null||rig.Gun==null||!rig.Gun.gameObject.activeInHierarchy||rig.Muzzle==null)return false;
            position=rig.Muzzle.position;direction=rig.Muzzle.forward;return true;
        }
        private static Vector3 SeatPosition(int seat) => Quaternion.Euler(0, seat * (360f / RouletteRules.MaxPlayers), 0) * (Vector3.back * SeatRadius);
        private static Quaternion SeatRotation(int seat) => Quaternion.Euler(0, 180 + seat * (360f / RouletteRules.MaxPlayers), 0);
        // Puppets face local -Z, so their anatomical right is local -X.
        // The grip lies toward the right hand; the barrel points inward across the table.
        private static Vector3 Rest(int seat) => SeatPosition(seat) + SeatRotation(seat) * new Vector3(-0.48f, 1.035f, -0.61f+DuelGunplay.SeatAdvance);
        private static Quaternion RestRotation(int seat) => SeatRotation(seat) * Quaternion.Euler(0, 160, 90);
        private void UseRig(int view, DuelWeapon weapon)
        {
            int selected=(int)weapon;
            for(int i=0;i<WeaponRules.Count;i++)if(i!=selected){var other=rigs[view,i];other.Gun.gameObject.SetActive(false);other.Loading.gameObject.SetActive(false);other.Light.intensity=0;}
            var r = rigs[view, selected]; gun = r.Gun; barrelPivot = r.Barrel; muzzle = r.Muzzle; holdingHand = r.Holding;
            loadingHand = r.Loading; flash = r.Flash; muzzleLight = r.Light; cartridges = r.Cartridges;
            hammer = r.Hammer; barrelLatch = r.Latch;
        }
        private void UpdateCosmetics(int seat,int packed)
        {
            if(cosmeticCache[seat]==packed)return;cosmeticCache[seat]=packed;
            for(int i=0;i<WeaponRules.Selectable.Length;i++)
                DuelWeaponSkins.Apply(rigs[seat,(int)WeaponRules.Selectable[i]].Gun,WeaponRules.Selectable[i],DuelSkins.WeaponFromPacked(packed,i));
            int taser=DuelSkins.FromPacked(packed,(int)DuelItem.Taser),noru=DuelSkins.FromPacked(packed,(int)DuelItem.NoruFace);
            var slide=rigs[seat,(int)DuelWeapon.Taser].Gun.Find("Taser slide");
            if(slide!=null){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",taser==0?new Color(.68f,.56f,.13f):DuelSkins.Tint(taser));foreach(var renderer in rigs[seat,(int)DuelWeapon.Taser].Gun.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial!=null&&renderer.sharedMaterial.name=="Taser safety yellow")renderer.SetPropertyBlock(block);}
            var taserRig=rigs[seat,(int)DuelWeapon.Taser];Color sparkColor=taser>0?DuelSkinEffects.Glow(taser):new Color(.2f,.7f,1);
            taserRig.Light.color=sparkColor;var sparkMaterial=taserRig.Flash.GetComponent<Renderer>().sharedMaterial;
            if(sparkMaterial!=null){sparkMaterial.SetColor("_BaseColor",taser>0?sparkColor:new Color(.3f,.8f,1));sparkMaterial.SetColor("_EmissionColor",taser>0?sparkColor*4f:new Color(1,4,7));}
            if(noruFabrics[seat]!=null)noruFabrics[seat].SetColor("_BaseColor",noru==0?new Color(.09f,.16f,.21f):DuelSkins.Tint(noru)*.5f);
        }
        public GameObject CloneTaserForPreview()
        {
            var clone=Instantiate(rigs[0,(int)DuelWeapon.Taser].Gun.gameObject);
            var hand=clone.transform.Find("Grip hand");if(hand!=null)Destroy(hand.gameObject);
            foreach(var light in clone.GetComponentsInChildren<Light>(true))light.enabled=false;
            clone.SetActive(true);return clone;
        }
        public GameObject CloneWeaponForPreview(DuelWeapon weapon,int skin)
        {
            if(!WeaponRules.IsSelectable(weapon)||rigs[0,(int)weapon]==null)return null;
            var clone=Instantiate(rigs[0,(int)weapon].Gun.gameObject);
            foreach(var hand in clone.GetComponentsInChildren<Transform>(true))
                if(hand.name.Contains("Grip hand")||hand.name=="BAS right grip"||hand.name=="BAS left support")hand.gameObject.SetActive(false);
            foreach(var light in clone.GetComponentsInChildren<Light>(true))light.enabled=false;
            DuelWeaponSkins.Apply(clone.transform,weapon,skin);clone.SetActive(true);return clone;
        }
        private void BuildTaser(Material black,Material steel,Material brass)
        {
            gun=new GameObject("Shockmaker taser").transform;gun.SetParent(world,false);gun.localScale=new Vector3(.58f,.64f,.78f);
            var yellow=Mat("Taser safety yellow",new Color(.68f,.56f,.13f),.1f,.3f);
            var shell=Faceted("Taser slide",gun,new Vector3(0,.03f,.02f),Vector3.one,yellow,
                new Vector4(-.25f,.085f,.08f,0),new Vector4(-.20f,.12f,.11f,0),new Vector4(.16f,.12f,.11f,0),new Vector4(.25f,.10f,.085f,0));shell.localRotation=Quaternion.Euler(90,0,0);
            var grip=Faceted("Taser grip",gun,new Vector3(0,-.22f,-.17f),Vector3.one,black,
                new Vector4(-.19f,.075f,.075f,0),new Vector4(-.14f,.085f,.08f,0),new Vector4(.10f,.075f,.075f,-.025f),new Vector4(.18f,.07f,.07f,-.045f));grip.localRotation=Quaternion.Euler(-8,0,0);
            Part("Cartridge",PrimitiveType.Cube,gun,new Vector3(0,.045f,.28f),new Vector3(.21f,.17f,.15f),black);
            Part("Cartridge face",PrimitiveType.Cube,gun,new Vector3(0,.045f,.358f),new Vector3(.175f,.125f,.014f),steel);
            foreach(float x in new[]{-.057f,.057f}){var socket=Part("Electrode socket",PrimitiveType.Cylinder,gun,new Vector3(x,.06f,.37f),new Vector3(.035f,.008f,.035f),black);socket.localRotation=Quaternion.Euler(90,0,0);var electrode=Part("Contact electrode",PrimitiveType.Cylinder,gun,new Vector3(x,.06f,.38f),new Vector3(.016f,.012f,.016f),steel);electrode.localRotation=Quaternion.Euler(90,0,0);}
            foreach(float side in new[]{-1f,1f})
            {
                Part("Side panel",PrimitiveType.Cube,gun,new Vector3(side*.113f,.03f,-.015f),new Vector3(.009f,.12f,.25f),black);
                Part("Side panel inset",PrimitiveType.Cube,gun,new Vector3(side*.119f,.033f,-.015f),new Vector3(.004f,.085f,.19f),yellow);
                foreach(float z in new[]{-.10f,-.065f,-.03f,.005f,.04f})Part("Cooling vent",PrimitiveType.Cube,gun,new Vector3(side*.123f,.045f,z),new Vector3(.005f,.035f,.009f),black);
                Part("Safety switch",PrimitiveType.Cube,gun,new Vector3(side*.12f,.10f,-.15f),new Vector3(.012f,.022f,.04f),steel);
                for(int j=0;j<4;j++)Part("Grip texture",PrimitiveType.Cube,gun,new Vector3(side*.079f,-.17f-j*.045f,-.17f),new Vector3(.006f,.008f,.11f),steel);
            }
            var trigger=Part("Taser trigger",PrimitiveType.Cube,gun,new Vector3(0,-.11f,-.055f),new Vector3(.018f,.09f,.022f),steel);trigger.localRotation=Quaternion.Euler(12,0,0);
            Part("Trigger guard front",PrimitiveType.Cube,gun,new Vector3(0,-.14f,.012f),new Vector3(.022f,.11f,.022f),black);
            Part("Trigger guard bottom",PrimitiveType.Cube,gun,new Vector3(0,-.195f,-.055f),new Vector3(.022f,.022f,.14f),black);
            Part("Front sight",PrimitiveType.Cube,gun,new Vector3(0,.16f,.20f),new Vector3(.025f,.035f,.035f),steel);
            Part("Rear sight",PrimitiveType.Cube,gun,new Vector3(0,.145f,-.18f),new Vector3(.07f,.04f,.03f),black);
            barrelPivot=new GameObject("Taser rigid barrel").transform;barrelPivot.SetParent(gun,false);
            hammer=new GameObject("Taser hammer placeholder").transform;hammer.SetParent(gun,false);
            barrelLatch=new GameObject("Taser latch placeholder").transform;barrelLatch.SetParent(gun,false);
            muzzle=new GameObject("Taser muzzle").transform;muzzle.SetParent(gun,false);muzzle.localPosition=new Vector3(0,.075f,.35f);
            cartridges[0]=new GameObject("Taser cartridge marker").transform;cartridges[0].SetParent(gun,false);
            var glow=Mat("Taser spark",new Color(.3f,.8f,1),0,0);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,4,7));
            flash=Part("Taser spark",PrimitiveType.Sphere,muzzle,Vector3.zero,Vector3.one*.08f,glow).gameObject;flash.SetActive(false);
            muzzleLight=muzzle.gameObject.AddComponent<Light>();muzzleLight.color=new Color(.2f,.7f,1);muzzleLight.range=2;
        }
        private void ShowHealing(int seat,DuelActionState action,double now,bool enabled)
        {
            bool show=enabled&&action.HealAt>=0&&now-action.HealAt<1.1;
            var ring=healRings[seat];if(!show){if(ring!=null)ring.enabled=false;for(int i=0;i<3;i++)if(rareHealRings[seat,i]!=null)rareHealRings[seat,i].enabled=false;return;}
            if(ring==null)
            {var go=new GameObject("Healing aurora "+seat);go.transform.SetParent(world,false);ring=go.AddComponent<LineRenderer>();healRings[seat]=ring;var mat=new Material(Resources.Load<Shader>("DuelBullet"));mat.SetColor("_BaseColor",new Color(.15f,1,.4f,1));materials.Add(mat);ring.sharedMaterial=mat;ring.useWorldSpace=true;ring.loop=true;ring.positionCount=40;ring.startWidth=ring.endWidth=.04f;ring.shadowCastingMode=ShadowCastingMode.Off;}
            int skin=DuelSkins.FromPacked(HelloWorldPlayer.SkinsForSeat(seat),(int)DuelItem.Recovery);ring.sharedMaterial.SetColor("_BaseColor",skin==0?new Color(.15f,1,.4f,1):DuelSkins.Tint(skin));
            float age=(float)(now-action.HealAt);ring.enabled=true;ring.startColor=ring.endColor=new Color(1,1,1,Mathf.Clamp01(1-age));
            for(int j=0;j<40;j++){float angle=j*Mathf.PI*2/40+age*3;ring.SetPosition(j,SeatPosition(seat)+new Vector3(Mathf.Cos(angle)*.43f,.95f+age*.65f+Mathf.Sin(angle*3+age*6)*.1f,Mathf.Sin(angle)*.43f));}
            int tier=DuelSkinEffects.Tier(skin);
            for(int i=0;i<3;i++)
            {
                bool active=tier>0&&i<tier-1;var halo=rareHealRings[seat,i];
                if(!active){if(halo!=null)halo.enabled=false;continue;}
                if(halo==null){var go=new GameObject("Rare healing spiral "+seat+"-"+i);go.transform.SetParent(world,false);halo=go.AddComponent<LineRenderer>();rareHealRings[seat,i]=halo;var mat=new Material(Resources.Load<Shader>("DuelBullet"));mat.SetColor("_BaseColor",Color.white);materials.Add(mat);halo.sharedMaterial=mat;halo.useWorldSpace=true;halo.loop=true;halo.positionCount=40;halo.shadowCastingMode=ShadowCastingMode.Off;}
                halo.enabled=true;halo.startWidth=halo.endWidth=tier>=3?.024f+.004f*tier:.014f+.003f*tier;Color glow=DuelSkinEffects.Glow(skin);glow.a=Mathf.Clamp01(1-age)*.75f;halo.startColor=halo.endColor=glow;
                for(int j=0;j<40;j++){float angle=j*Mathf.PI*2/40-age*(3+i)+i*2.1f;float radius=.30f+i*.08f;halo.SetPosition(j,SeatPosition(seat)+new Vector3(Mathf.Cos(angle)*radius,.95f+age*.60f+i*.13f+Mathf.Sin(angle*2+age*5)*.09f,Mathf.Sin(angle)*radius));}
            }
        }
        private void ShowStun(int seat,double now,bool enabled,int skin)
        {
            var puppet=puppets[seat];
            for(int arc=0;arc<3;arc++)
            {
                var line=stunArcs[seat,arc];
                if(!enabled){if(line!=null)line.enabled=false;continue;}
                if(line==null)
                {
                    var go=new GameObject("Stun electricity "+seat+"-"+arc);go.transform.SetParent(world,false);
                    line=go.AddComponent<LineRenderer>();stunArcs[seat,arc]=line;
                    var mat=new Material(Resources.Load<Shader>("DuelBullet"));mat.SetColor("_BaseColor",new Color(.3f,.8f,1,1));materials.Add(mat);
                    line.sharedMaterial=mat;line.useWorldSpace=true;line.positionCount=9;
                    line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
                }
                line.enabled=true;float tick=Mathf.Floor((float)now*18);
                float pulse=.65f+.35f*Mathf.Sin((float)now*37+arc*2+seat);
                line.startWidth=line.endWidth=.012f+.012f*pulse;
                Color electric=skin>0?DuelSkinEffects.Glow(skin):new Color(.7f,.9f,1);electric.a=pulse;line.sharedMaterial.SetColor("_BaseColor",skin>0?Color.white:new Color(.3f,.8f,1,1));line.startColor=line.endColor=electric;
                for(int j=0;j<9;j++)
                {
                    float t=j/8f,angle=arc*Mathf.PI*2/3+t*6+tick*.7f+seat;
                    float radius=.22f+.07f*Mathf.Sin(tick*3+j*7+arc);
                    var center=Vector3.Lerp(puppet.Torso.position-Vector3.up*.22f,puppet.Head.position+Vector3.up*.15f,t);
                    line.SetPosition(j,center+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(tick+j*13)*.045f,Mathf.Sin(angle)*radius));
                }
            }
        }
        private void BuildSingleShot(DuelWeapon weapon, Material black, Material steel, Material wood, Material brass)
        {
            if(weapon==DuelWeapon.Taser){BuildTaser(black,steel,brass);return;}
            if(weapon==DuelWeapon.BAS){BuildBas(black,steel,brass);return;}
            bool saw = weapon == DuelWeapon.SawedOff;
            if(!saw){BuildRevolver(black,steel,brass);return;}
            float length = saw ? 0.86f : 0.38f, diameter = saw ? 0.15f : 0.10f;
            gun = new GameObject(saw ? "Single-barrel break-action sawed-off" : "Single-barrel derringer").transform;
            gun.SetParent(world, false);
            gun.localScale = Vector3.one * 0.78f;
            Part("Breech frame", PrimitiveType.Cube, gun, new Vector3(0, 0, -0.15f), new Vector3(saw ? 0.19f : 0.13f, 0.19f, 0.30f), steel);
            Part("Breech face", PrimitiveType.Cube, gun, new Vector3(0, 0.075f, -0.04f), new Vector3(diameter, 0.13f, 0.028f), black);
            barrelPivot = new GameObject("Break-action hinge").transform; barrelPivot.SetParent(gun, false);
            barrelPivot.localPosition = new Vector3(0, -0.045f, 0);
            Axial("Single barrel", barrelPivot, new Vector3(0, 0.12f, length / 2), diameter, length, black);
            Axial("Breech opening", barrelPivot, new Vector3(0, 0.12f, -0.004f), diameter * .7f, 0.006f, black);
            Ring("Single chamber rim", barrelPivot, new Vector3(0, 0.12f, -0.008f), diameter * .44f, 0.008f, steel, Quaternion.identity);
            Axial("Dark bore", barrelPivot, new Vector3(0, 0.12f, length + .003f), diameter * .70f, .007f, black);
            Ring("Muzzle crown", barrelPivot, new Vector3(0, 0.12f, length + .008f), diameter * .44f, .008f, steel, Quaternion.identity);
            Part("Front sight", PrimitiveType.Cube, barrelPivot, new Vector3(0, 0.12f + diameter * .6f, length - .045f), new Vector3(.025f, .035f, .04f), steel);
            float sightHeight = .075f + diameter * .6f + .0175f;
            foreach (float x in new[]{-.035f,.035f})
                Part("Rear sight notch", PrimitiveType.Cube, gun, new Vector3(x,sightHeight-.018f,-.20f), new Vector3(.022f,.055f,.04f), black);
            if (saw) Part("Front bead", PrimitiveType.Sphere, barrelPivot,
                new Vector3(0,.12f+diameter*.6f+.0175f,length-.045f),Vector3.one*.029f,brass);
            Part("Barrel rib", PrimitiveType.Cube, barrelPivot, new Vector3(0, 0.12f + diameter * .48f, length * .5f), new Vector3(.025f, .02f, length), steel);
            if (saw) Part("Short walnut fore-end", PrimitiveType.Cube, barrelPivot, new Vector3(0, -.015f, .28f), new Vector3(.17f, .11f, .38f), wood);
            var grip = Part(saw ? "Cut walnut stock" : "Compact walnut grip", PrimitiveType.Cube, gun,
                new Vector3(0, -0.24f, saw ? -.38f : -.23f), new Vector3(.17f, saw ? .20f : .29f, saw ? .44f : .18f), wood);
            grip.localRotation = Quaternion.Euler(saw ? -35 : -16, 0, 0);
            Axial("Hinge pin", gun, new Vector3(0, -.045f, 0), .085f, .18f, steel).localRotation = Quaternion.Euler(0, 0, 90);
            cartridges[0] = new GameObject(saw ? "One shotgun shell" : "One pistol cartridge").transform;
            cartridges[0].SetParent(barrelPivot, false);
            Axial("Single cartridge body", cartridges[0], Vector3.zero, saw ? .09f : .052f, saw ? .20f : .13f, saw ? shellRed : brass);
            Axial("Brass cartridge head", cartridges[0], new Vector3(0, 0, saw ? -.10f : -.065f), saw ? .10f : .058f, .025f, brass);
            if (!saw) Part("Bullet tip", PrimitiveType.Sphere, cartridges[0], new Vector3(0, 0, .08f), new Vector3(.052f, .052f, .07f), liveTip);
            cartridges[0].gameObject.SetActive(false);
            Ring("Trigger guard", gun, new Vector3(0, -0.20f, -0.035f), 0.105f, 0.016f, steel, Quaternion.Euler(0, 90, 0));
            var trigger = Part("Trigger", PrimitiveType.Cube, gun, new Vector3(0, -0.19f, -0.08f), new Vector3(0.025f, 0.11f, 0.026f), black);
            trigger.localRotation = Quaternion.Euler(-24, 0, 0);
            hammer = new GameObject("Hammer pivot").transform; hammer.SetParent(gun, false);
            hammer.localPosition = new Vector3(0, .075f, -.28f);
            Part("Hammer", PrimitiveType.Cube, hammer, new Vector3(0, .065f, -.015f), new Vector3(.055f, .10f, .065f), black);
            Part("Hammer thumb spur", PrimitiveType.Cube, hammer, new Vector3(0, .11f, -.05f), new Vector3(.08f, .025f, .09f), steel);
            // The sawed-off uses an internal striker; an exposed pistol hammer blocks its sight line.
            hammer.gameObject.SetActive(!saw);
            barrelLatch = new GameObject("Barrel locking lever pivot").transform; barrelLatch.SetParent(gun, false);
            barrelLatch.localPosition = new Vector3(.10f, .015f, -.15f);
            Part("Barrel locking lever", PrimitiveType.Cube, barrelLatch, new Vector3(0, .035f, .035f), new Vector3(.025f, .065f, .12f), steel);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(barrelPivot, false); muzzle.localPosition = new Vector3(0, .12f, length + .04f);
            var flashMaterial = Mat("Muzzle light", new Color(1, 0.7f, 0.15f), 0, 0);
            flashMaterial.EnableKeyword("_EMISSION"); flashMaterial.SetColor("_EmissionColor", new Color(8, 3, 0.3f));
            flash = Part("Muzzle flash", PrimitiveType.Sphere, muzzle, Vector3.zero, new Vector3(0.10f, 0.10f, 0.24f), flashMaterial).gameObject;
            muzzleLight = muzzle.gameObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point; muzzleLight.color = new Color(1, 0.6f, 0.18f); muzzleLight.range = 3;
            flash.SetActive(false);
        }

        private static Vector3 ChamberPosition(int chamber)
        {float a=chamber*Mathf.PI/3;return new Vector3(.085f*Mathf.Sin(a),.085f*Mathf.Cos(a),-.015f);}
        private void BuildRevolver(Material black,Material steel,Material brass)
        {
            var frame=Mat("Revolver bronze frame",new Color(.30f,.24f,.17f),.85f,.45f);
            gun=new GameObject("Six-shot single-action revolver").transform;gun.SetParent(world,false);gun.localScale=new Vector3(.58f,.64f,.78f);
            Part("Top frame strap",PrimitiveType.Cube,gun,new Vector3(0,.145f,-.015f),new Vector3(.17f,.035f,.42f),frame);
            Part("Lower frame",PrimitiveType.Cube,gun,new Vector3(0,-.14f,-.055f),new Vector3(.16f,.035f,.36f),frame);
            Part("Recoil shield",PrimitiveType.Sphere,gun,new Vector3(-.045f,-.005f,-.20f),new Vector3(.18f,.32f,.09f),frame);
            Part("Frame neck",PrimitiveType.Cube,gun,new Vector3(0,-.08f,-.245f),new Vector3(.15f,.22f,.16f),frame);
            Axial("Long blued barrel",gun,new Vector3(0,.075f,.42f),.10f,.60f,black);
            Axial("Muzzle bore",gun,new Vector3(0,.075f,.722f),.067f,.005f,black);
            Ring("Muzzle crown",gun,new Vector3(0,.075f,.725f),.043f,.009f,steel,Quaternion.identity);
            Part("Front blade sight",PrimitiveType.Cube,gun,new Vector3(0,.14f,.67f),new Vector3(.025f,.03f,.08f),steel);
            foreach(float x in new[]{-.035f,.035f})Part("Rear sight notch",PrimitiveType.Cube,gun,new Vector3(x,.134f,-.205f),new Vector3(.022f,.045f,.035f),black);
            Axial("Ejector housing",gun,new Vector3(.06f,-.005f,.40f),.038f,.54f,steel);
            Axial("Ejector rod",gun,new Vector3(.06f,-.005f,.20f),.016f,.32f,black);
            Part("Ejector button",PrimitiveType.Sphere,gun,new Vector3(.06f,-.005f,.12f),new Vector3(.055f,.045f,.035f),black);
            barrelPivot=new GameObject("Six-chamber cylinder pivot").transform;barrelPivot.SetParent(gun,false);barrelPivot.localPosition=new Vector3(0,-.01f,-.02f);
            Axial("Fluted cylinder",barrelPivot,Vector3.zero,.264f,.29f,steel);
            for(int i=0;i<6;i++)
            {
                Vector3 p=ChamberPosition(i);
                Axial("Cylinder flute "+i,barrelPivot,new Vector3(p.x*1.46f,p.y*1.46f,0),.025f,.19f,black);
                Axial("Empty chamber "+i,barrelPivot,new Vector3(p.x,p.y,-.149f),.047f,.006f,black);
                Ring("Chamber rim "+i,barrelPivot,new Vector3(p.x,p.y,-.15f),.026f,.004f,steel,Quaternion.identity);
                cartridges[i]=new GameObject("Revolver cartridge "+i).transform;cartridges[i].SetParent(barrelPivot,false);cartridges[i].localPosition=p;
                Axial("Cartridge brass",cartridges[i],Vector3.zero,.041f,.23f,brass);
                Axial("Case head",cartridges[i],new Vector3(0,0,-.14f),.047f,.017f,brass);
                Part("Bullet tip",PrimitiveType.Sphere,cartridges[i],new Vector3(0,0,.115f),new Vector3(.041f,.041f,.05f),liveTip);cartridges[i].gameObject.SetActive(false);
            }
            var grip=Part("Curved black grip",PrimitiveType.Capsule,gun,new Vector3(0,-.30f,-.34f),new Vector3(.17f,.18f,.20f),black);grip.localRotation=Quaternion.Euler(-25,0,0);
            Part("Grip butt",PrimitiveType.Sphere,gun,new Vector3(0,-.44f,-.40f),new Vector3(.18f,.075f,.24f),black);
            foreach(float x in new[]{-.087f,.087f})
            {
                var badge=Part("Grip medallion",PrimitiveType.Cylinder,gun,new Vector3(x,-.21f,-.30f),new Vector3(.048f,.004f,.048f),steel);badge.localRotation=Quaternion.Euler(0,0,90);
                Part("Frame screw",PrimitiveType.Sphere,gun,new Vector3(x,-.06f,-.25f),Vector3.one*.027f,steel);
            }
            Ring("Trigger guard",gun,new Vector3(0,-.235f,-.155f),.09f,.014f,steel,Quaternion.Euler(0,90,0));
            Part("Trigger",PrimitiveType.Cube,gun,new Vector3(0,-.19f,-.17f),new Vector3(.026f,.09f,.04f),black);
            hammer=new GameObject("Hammer pivot").transform;hammer.SetParent(gun,false);hammer.localPosition=new Vector3(0,.04f,-.25f);
            Part("Hammer",PrimitiveType.Cube,hammer,new Vector3(0,.06f,0),new Vector3(.055f,.10f,.06f),steel);
            Part("Hammer thumb spur",PrimitiveType.Cube,hammer,new Vector3(0,.11f,-.05f),new Vector3(.07f,.025f,.10f),steel);
            barrelLatch=new GameObject("Loading gate pivot").transform;barrelLatch.SetParent(gun,false);barrelLatch.localPosition=new Vector3(.135f,-.005f,-.19f);
            Part("Loading gate",PrimitiveType.Sphere,barrelLatch,new Vector3(-.035f,0,-.018f),new Vector3(.10f,.15f,.055f),frame);
            muzzle=new GameObject("Muzzle").transform;muzzle.SetParent(gun,false);muzzle.localPosition=new Vector3(0,.075f,.72f);
            var material=Mat("Revolver muzzle light",new Color(1,.7f,.15f),0,0);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(8,3,.3f));
            flash=Part("Muzzle flash",PrimitiveType.Sphere,muzzle,Vector3.zero,new Vector3(.10f,.10f,.24f),material).gameObject;
            muzzleLight=muzzle.gameObject.AddComponent<Light>();muzzleLight.type=LightType.Point;muzzleLight.color=new Color(1,.6f,.18f);muzzleLight.range=3;flash.SetActive(false);
        }
        private DuelViewModelLayer viewModelLayer;
        private void LateUpdate()
        {
            if(viewModelLayer==null&&viewCamera!=null)viewModelLayer=new DuelViewModelLayer(viewCamera,transform);
            viewModelLayer?.BeginFrame();
            var player = HelloWorldPlayer.Local;
            bool ready = player != null && player.IsSpawned && player.Seat.Value >= 0;
            var state = ready ? player.State.Value : default; int localSeat = ready ? player.Seat.Value : 0;
            double now = ready ? player.NetworkManager.ServerTime.Time : Time.timeAsDouble;
            var spectator=GetComponent<DuelSpectator>();bool watching=ready&&spectator!=null&&spectator.Watching;
            int viewSeat=watching?spectator.TargetSeat:localSeat;
            bool viewActive=watching||firstPerson!=null&&firstPerson.Active||ready&&state.CanPreviewEquipment(localSeat)&&state.ForSeat(localSeat).PreviewItem!=0;
            bool viewAds=watching?spectator.Ads(state):firstPerson!=null&&firstPerson.Ads;
            drinkVisuals.Observe(ready?state.Tabletop:default,ready?state.Round:0,now,weaponAudio,ready);
            if(!ready){shotVisuals.Clear();weaponAudio.Clear();}
            double lastShotAt = -1;
            bool ownPractice=ready&&state.IsPracticing(localSeat);
            bool ownDeath = ready && !ownPractice&&state.ForSeat(localSeat).DiedAt >= 0;
            double ownHitAt = ready ? state.ForSeat(localSeat).HitAt : -1;
            bool shared=ready && state.PracticeMask==0&&state.Clock.Phase!=ClockPhase.QuickHands && state.Clock.Phase!=ClockPhase.QuickCountdown && !(state.Clock.Phase==ClockPhase.ItemPreparation&&state.Clock.MiniGame==PartyMiniGame.QuickHands) && !(state.Phase==DuelPhase.Finished && state.Clock.MiniGame==PartyMiniGame.QuickHands);
            int holder=shared && (state.Clock.Phase==ClockPhase.Shooting || (state.Clock.Phase==ClockPhase.Returning && state.Clock.First>=0 && now-state.ForSeat(state.Clock.First).FiredAt<.3))?state.Clock.First:-1;
            HitPulse = ownHitAt >= 0 ? Mathf.Exp(-Mathf.Max(0, (float)(now - ownHitAt)) * 5) : 0;
            for (int seat = 0; seat < RouletteRules.MaxPlayers; seat++)
            {
                var action = state.ForSeat(seat);
                UpdateCosmetics(seat, ready ? HelloWorldPlayer.SkinsForSeat(seat) : 0);
                bool practice=ready&&state.IsPracticing(seat);
                bool preview=ready&&state.CanPreviewEquipment(seat)&&action.PreviewItem!=0;
                bool previewInk=preview&&action.PreviewItem==(int)DuelWheelSlot.Ink;
                bool previewTaser=preview&&action.PreviewItem==(int)DuelWheelSlot.Taser;
                if(previewTaser){action.Weapon=DuelWeapon.Taser;action.Action=DuelAction.Aim;action.LoadedMask=1;}
                bool inkPose=previewInk||ready&&(practice||state.Alive(seat)&&state.Stage==DuelStage.Racing&&
                    (DuelInk.Usable(state.Clock.Phase)||state.Clock.Phase==ClockPhase.QuickCountdown))&&DuelInk.Busy(action.Ink,now);
                UseRig(seat, ready ? action.Weapon : DuelWeapon.Revolver); var puppet = puppets[seat];
                bool occupied = !ready || state.Connected(seat);
                chairs[seat].gameObject.SetActive(true);
                puppet.Root.gameObject.SetActive(occupied);
                ShowNoruFace(seat,ready&&occupied&&(practice||state.Alive(seat))&&action.NoruFaceActive&&
                    (practice||state.Clock.Phase==ClockPhase.ItemPreparation||state.Clock.Phase==ClockPhase.QuickCountdown||DuelInk.Shooting(state.Clock.Phase)));
                gun.gameObject.SetActive(occupied && (previewTaser||!shared || seat==holder || action.TaserArmed&&state.Clock.Phase==ClockPhase.Shooting));
                ShowHealing(seat,action,now,ready&&(practice||state.Clock.Phase==ClockPhase.ItemPreparation)); loadingHand.gameObject.SetActive(false);
                if (!occupied) { rigs[seat,3].EjectedCase.gameObject.SetActive(false);rigs[seat,3].BasSeen=false;muzzleLight.intensity = 0;weaponAudio.Observe(seat,state.Round,state.Clock.Iteration,action,now,false,false,false,Vector3.zero);continue; }
                if (ready) lastShotAt = System.Math.Max(lastShotAt, action.FiredAt);
                float elapsed = Mathf.Max(0, (float)(now - action.FiredAt));
                bool dead = ready && action.DiedAt >= 0;
                float fall = dead ? Smooth((float)(now - action.DiedAt) / FallDuration) : 0;
                float recovery=dead&&action.LivesRemaining>0&&action.ReviveAt>=0?Mathf.Clamp01((float)((now-action.ReviveAt)/DuelDirector.RecoveryDuration)):0;
                fall*=1-Smooth(recovery);
                bool active = !inkPose&&(previewTaser||practice||ready && (!shared || seat==holder || action.TaserArmed&&state.Clock.Phase==ClockPhase.Shooting) && state.Alive(seat) && (state.Stage == DuelStage.Racing || state.Stage == DuelStage.AfterShot));
                bool shooter = active && action.FiredAt >= 0 && elapsed < 0.3f;
                float progress = action.Progress / (float)action.Length;
                bool revolver=action.Weapon==DuelWeapon.Revolver;
                bool manualAim=revolver&&(action.Aiming || seat==localSeat&&firstPerson!=null&&firstPerson.Active&&firstPerson.Ads);
                float lift = active ? (action.Action == DuelAction.Pickup ? progress : 1) : 0;
                if(active&&manualAim)lift=1;
                if (shooter) lift = 1;
                bool gunReady = active && (action.Action == DuelAction.Aim||manualAim);
                float aim = shooter || gunReady ? 1 : 0;
                Vector3 held = SeatPosition(seat) + SeatRotation(seat) * new Vector3(-0.18f, 1.40f, -0.44f);
                Quaternion inspection = SeatRotation(seat) * Quaternion.Euler(-22, 155, 0);
                if(active&&!gunReady)
                {
                    DuelCharacterPose.Preparation(action.Action,action.Weapon,progress,out var poseOffset,out var poseEuler);
                    held+=SeatRotation(seat)*poseOffset;inspection=SeatRotation(seat)*Quaternion.Euler(new Vector3(-22,155,0)+poseEuler);
                    if(action.Weapon==DuelWeapon.BAS)inspection=SeatRotation(seat)*Quaternion.Euler(-5,145,-15);
                }
                DuelGunplay.Sway(seat,action.Weapon,action.Aiming,now,action.AimStartedAt,out float swayYaw,out float swayPitch);
                Quaternion aimed = Quaternion.Euler(-action.AimPitch-swayPitch,action.AimYaw+swayYaw,0);
                float recoil = shooter ? Mathf.Sin(Mathf.Clamp01(elapsed / 0.3f) * Mathf.PI) * 10 : 0;
                var rotation = Quaternion.Slerp(inspection, aimed, aim) * Quaternion.Euler(-recoil, 0, 0);
                float follow = 1 - Mathf.Exp(-Time.deltaTime * 16);
                gun.localPosition = Vector3.Lerp(gun.localPosition, Vector3.Lerp(Rest(seat), held, lift), follow);
                gun.localRotation = Quaternion.Slerp(gun.localRotation, Quaternion.Slerp(RestRotation(seat), rotation, lift), follow);
                if(gunReady || shooter)
                {
                    var facing=aimed;
                    var centre=DuelGunplay.Direction(action.AimYaw+swayYaw,action.AimPitch+swayPitch);
                    var origin=DuelGunplay.RenderMuzzleOrigin(seat,action.Weapon,action.Aiming,centre);
                    float barrelLength=action.Weapon==DuelWeapon.BAS?1.34f:action.Weapon==DuelWeapon.SawedOff?.90f:action.Weapon==DuelWeapon.Taser?.35f:.72f;
                    float muzzleHeight=(action.Weapon==DuelWeapon.SawedOff?.12f:.075f)*gun.localScale.y;
                    gun.position=Vec(origin)-facing*new Vector3(0,muzzleHeight,barrelLength*gun.localScale.z);
                    gun.rotation=facing*Quaternion.Euler(-recoil,0,0);
                }
                if (seat == viewSeat && viewActive && (gunReady || shooter))
                {
                    var eye = Vec(DuelGunplay.Eye(seat)); var facing = watching?spectator.Rotation(state,now):firstPerson.Rotation(now);
                    float sightY = action.Weapon==DuelWeapon.BAS?.151f*.60f:action.Weapon==DuelWeapon.SawedOff?(.075f+.15f*.6f+.0175f)*.78f:.155f*.64f;
                    // Keep the stock/hammer in front of the near plane. A close gun origin clips the sight picture.
                    float sightDistance = action.Weapon == DuelWeapon.SawedOff ? .90f : action.Weapon==DuelWeapon.Taser?.5f:.85f;
                    Vector3 offset = viewAds ? new Vector3(0,-sightY,sightDistance) : new Vector3(.28f,-.23f,.75f);
                    gun.position = eye + facing * (offset + new Vector3(0,0,-recoil*.002f));
                    gun.rotation = facing * Quaternion.Euler(-recoil,0,0);
                    if(action.Weapon==DuelWeapon.BAS)PlaceBasView(rigs[seat,3],eye,facing,viewAds?offset:new Vector3(.25f,-.255f,.80f),Quaternion.Euler(-recoil,0,0));
                }
                if(action.Weapon==DuelWeapon.BAS&&active&&!gunReady&&!shooter&&seat==viewSeat&&viewActive)
                {
                    // A long rifle needs a sideways inspection pose; a pistol pose puts the stock through the camera.
                    var facing=Quaternion.Euler(watching?-spectator.Pitch:-firstPerson.Pitch,watching?spectator.Yaw:firstPerson.Yaw,0);
                    var angle=action.Action==DuelAction.Load?new Vector3(7,-48,-18):action.Action==DuelAction.OpenBarrel?new Vector3(6,-42,-12-4*Mathf.Sin(progress*Mathf.PI)):new Vector3(5,-40,-10);
                    PlaceBasView(rigs[seat,3],Vec(DuelGunplay.Eye(seat)),facing,new Vector3(.20f,-.30f,.86f),Quaternion.Euler(angle));
                }
                if(action.Weapon==DuelWeapon.BAS&&!(seat==viewSeat&&viewActive&&active))rigs[seat,3].ViewPoseInitialized=false;
                holdingHand.gameObject.SetActive(lift > 0.05f);
                bool loading = active && !shooter && action.Action == DuelAction.Load;
                bool opening = active && !shooter && action.Action == DuelAction.OpenBarrel;
                bool closingBarrel = active && !shooter && action.Action == DuelAction.CloseBarrel;
                bool unlocking = active && !shooter && action.Action == DuelAction.UnlockBarrel;
                bool cocking = active && !shooter && action.Action == DuelAction.CockHammer;
                bool ejecting=revolver&&closingBarrel;
                float open = opening ? progress : loading ? 1 : closingBarrel ? 1 - progress : 0;
                int loadingSlot=revolver?WeaponRules.Chamber(63&~(action.LoadedMask|action.SpentMask),action.NextChamber):0;
                if(revolver)
                {
                    open=gunReady||shooter?0:opening?progress:loading||ejecting?1:0;
                    int chamber=shooter?(action.NextChamber+5)%6:WeaponRules.Chamber(action.LoadedMask,action.NextChamber);
                    int ejectSlot=-1,rank=0,count=RouletteRules.Count(action.SpentMask);
                    for(int i=0;i<6;i++)if((action.SpentMask&(1<<i))!=0){if(rank==Mathf.Min(count-1,Mathf.FloorToInt(progress*count)))ejectSlot=i;rank++;}
                    float cylinderAngle=loading&&!gunReady?Mathf.Max(0,loadingSlot)*60-90:ejecting?Mathf.Max(0,ejectSlot)*60-90:Mathf.Max(0,chamber)*60;
                    barrelPivot.localRotation=Quaternion.Slerp(barrelPivot.localRotation,Quaternion.Euler(0,0,cylinderAngle),follow);
                }
                else if(action.Weapon==DuelWeapon.BAS)
                {
                    float leverOpen=opening?progress:loading?1:closingBarrel?1-progress:0;
                    if(practice&&opening)leverOpen=Mathf.Sin(progress*Mathf.PI);
                    barrelPivot.localRotation=Quaternion.Slerp(barrelPivot.localRotation,Quaternion.Euler(-70*leverOpen,0,0),follow);
                    barrelLatch.localPosition=Vector3.Lerp(barrelLatch.localPosition,new Vector3(0,0,-.18f*leverOpen),follow);
                }
                else barrelPivot.localRotation = Quaternion.Slerp(barrelPivot.localRotation, Quaternion.Euler(50 * open, 0, 0), follow);
                float latchOpen = unlocking ? progress : opening || loading ? 1 : closingBarrel ? 1 - progress : 0;
                barrelLatch.localRotation = Quaternion.Slerp(barrelLatch.localRotation, action.Weapon==DuelWeapon.BAS?Quaternion.identity:revolver?Quaternion.Euler(0,-85*open,0):Quaternion.Euler(45*latchOpen,0,0), follow);
                float cock = action.Weapon == DuelWeapon.Revolver && active ?
                    (gunReady ? 1 : cocking ? progress*.5f : action.Action!=DuelAction.Pickup ? .5f : 0) : 0;
                if (shooter && action.Weapon == DuelWeapon.Revolver) cock = 1 - Smooth(elapsed / .08f);
                hammer.localRotation = Quaternion.Slerp(hammer.localRotation, Quaternion.Euler(-65 * cock, 0, 0), follow);
                foreach (var cartridge in cartridges) cartridge.gameObject.SetActive(false);
                loadingHand.gameObject.SetActive(!gunReady&&!shooter&&(action.Weapon==DuelWeapon.BAS?loading:opening || loading || closingBarrel || unlocking || cocking));
                if(revolver)
                {
                    int spentRank=0,spentCount=RouletteRules.Count(action.SpentMask);
                    for(int i=0;i<cartridges.Length;i++)
                    {
                        bool inserted=((action.LoadedMask|action.SpentMask)&(1<<i))!=0;
                        bool pending=loading&&!gunReady&&!shooter&&i==loadingSlot;
                        float exit=0;
                        if(ejecting&&!gunReady&&(action.SpentMask&(1<<i))!=0){exit=Mathf.Clamp01(progress*spentCount-spentRank);spentRank++;if(exit>=1)inserted=false;}
                        cartridges[i].gameObject.SetActive(inserted||pending);
                        cartridges[i].localPosition=ChamberPosition(i)+(pending?new Vector3(.04f,.04f,-.30f)*(1-progress):new Vector3(.10f*exit,.08f*Mathf.Sin(exit*Mathf.PI),-.45f*exit));
                        cartridges[i].localRotation=Quaternion.Euler(exit*100,pending?-25*(1-progress):exit*70,0);
                        cartridges[i].Find("Bullet tip").gameObject.SetActive(pending||(action.LoadedMask&(1<<i))!=0);
                    }
                }
                else if(action.Weapon==DuelWeapon.BAS)
                {
                    cartridges[0].gameObject.SetActive(loading||closingBarrel);
                    cartridges[0].localPosition=loading?Vector3.Lerp(new Vector3(.25f,.21f,-.18f),new Vector3(.015f,.075f,.02f),progress):new Vector3(.015f,.075f,.02f);
                    cartridges[0].localRotation=Quaternion.Euler(loading?-35*(1-progress):0,loading?25*(1-progress):0,0);
                    cartridges[0].Find("Bullet tip").gameObject.SetActive(!opening);
                }
                else if (opening&&action.SpentMask!=0)
                {
                    float exit=Smooth(Mathf.InverseLerp(.25f,.85f,progress));
                    cartridges[0].gameObject.SetActive(exit<1);
                    cartridges[0].localPosition=new Vector3(.12f*exit,.09f*Mathf.Sin(exit*Mathf.PI),-.05f-.50f*exit);
                    cartridges[0].localRotation=Quaternion.Euler(exit*120,exit*55,0);
                }
                else if (loading || closingBarrel)
                {
                    cartridges[0].gameObject.SetActive(true);
                    float insertion = loading ? progress : 1;
                    cartridges[0].localPosition = Vector3.Lerp(new Vector3(.12f, .22f, -.42f), new Vector3(0, .12f, .085f), insertion);
                    cartridges[0].localRotation = Quaternion.Euler(0, -30 * (1 - insertion), 0);
                }
                if (loadingHand.gameObject.activeSelf)
                {
                    loadingHand.position = unlocking ? barrelLatch.TransformPoint(new Vector3(.035f, .02f, .02f)) :
                        cocking ? hammer.TransformPoint(new Vector3(.05f, .11f, -.06f)) :
                        loading ? cartridges[Mathf.Max(0,loadingSlot)].TransformPoint(new Vector3(0, -.04f, -.14f)) :
                        barrelPivot.TransformPoint(new Vector3(.10f, -.07f, .23f));
                    if(ejecting)loadingHand.position=gun.TransformPoint(new Vector3(.06f,-.005f,.12f+.08f*Mathf.Sin(progress*Mathf.PI*3)));
                    loadingHand.rotation=(loading?cartridges[Mathf.Max(0,loadingSlot)].rotation:gun.rotation)*Quaternion.Euler(-10,70,0);
                }
                if(action.Weapon==DuelWeapon.BAS)
                {
                    holdingHand.position=opening||closingBarrel?barrelPivot.TransformPoint(new Vector3(.075f,-.125f,-.335f)):gun.TransformPoint(BasGripWrist);
                    holdingHand.rotation=opening||closingBarrel?barrelPivot.rotation:gun.rotation;
                    if(loading){loadingHand.position=cartridges[0].TransformPoint(new Vector3(-.14f,-.06f,-.12f));loadingHand.rotation=cartridges[0].rotation;}
                    rigs[seat,3].Support.gameObject.SetActive(holdingHand.gameObject.activeSelf&&!loading&&active&&!dead);
                }
                bool flashing = shooter && elapsed < 0.12f;
                if(inkPose){gun.gameObject.SetActive(false);holdingHand.gameObject.SetActive(false);loadingHand.gameObject.SetActive(false);}
                flash.SetActive(flashing); muzzleLight.intensity = flashing ? 8 : 0;
                float hitAge=ready&&action.HitAt>=0?(float)(now-action.HitAt):-1;
                float hit=hitAge>=0?Smooth(hitAge/.06f)*(1-Smooth((hitAge-.06f)/.34f)):0;
                float lookYaw=ready?action.AimYaw:seat*60, lookPitch=ready?action.AimPitch:0;
                if(seat==localSeat && firstPerson!=null && (firstPerson.Active||ready&&(state.Phase==DuelPhase.Waiting||state.Phase==DuelPhase.Playing&&state.Stage==DuelStage.Countdown&&state.Alive(seat))))
                {lookYaw=firstPerson.Yaw;lookPitch=firstPerson.Pitch;}
                var poseContext=new DuelPoseContext{Seat=seat,Time=now,Action=action.Action,Weapon=action.Weapon,Progress=progress,
                    Ink=inkPose,InkAge=action.Ink.ThrownAt>=0?(float)(now-action.Ink.ThrownAt):-1,
                    DrinkAction=ready&&!preview?state.Tabletop.ForSeat(seat).Action:DuelDrinkAction.None,
                    DrinkProgress=ready?Mathf.Clamp01((float)((now-state.Tabletop.ForSeat(seat).StartedAt)/DuelTabletop.Duration(state.Tabletop.ForSeat(seat).Action))):0,
                    Combat=practice||ready&&(state.Clock.Phase==ClockPhase.QuickHands||state.Clock.Phase==ClockPhase.Shooting),
                    ActionAge=Mathf.Max(0,(float)(now-action.StartedAt)),Active=active&&(practice||state.Clock.Phase==ClockPhase.QuickHands||state.Clock.Phase==ClockPhase.Shooting),GunReady=gunReady||shooter,Aiming=action.Aiming||manualAim||shooter&&action.FiredAds,
                    Recoil=shooter?recoil/10:0,Stunned=ready&&action.IsStunned(now),Attentive=ready&&state.Clock.Phase==ClockPhase.Counting,
                    Winner=ready&&!practice&&state.Phase==DuelPhase.Finished&&state.Winner==seat,
                    ViewModel=seat==viewSeat&&viewActive&&(previewInk||gunReady||shooter||action.Weapon==DuelWeapon.BAS&&active),
                    Healing=ready&&(practice||state.Clock.Phase==ClockPhase.ItemPreparation)&&action.HealAt>=0?Mathf.Clamp01(1-(float)(now-action.HealAt)/1.1f):0};
                AnimatePuppetFrame(puppet, lift, hit, fall, lookYaw, lookPitch,recovery,poseContext);
                if(poseContext.ViewModel&&viewModelLayer!=null)
                {
                    viewModelLayer.Include(gun);viewModelLayer.Include(loadingHand);
                    viewModelLayer.Include(puppet.Upper);viewModelLayer.Include(puppet.Forearm);viewModelLayer.Include(puppet.Hand);
                    viewModelLayer.Include(puppet.LeftArm);viewModelLayer.Include(puppet.LeftForearm);viewModelLayer.Include(puppet.LeftHand);
                }
                ObserveBasCase(rigs[seat,3],action,state.Round,now,ready&&action.Weapon==DuelWeapon.BAS&&!dead);

                ShowStun(seat,now,ready&&(practice||state.Alive(seat)&&DuelInk.Shooting(state.Clock.Phase))&&action.IsStunned(now)&&
                    !(seat==viewSeat&&viewActive),ready?DuelSkinEffects.SourceSkin(state,seat,DuelItem.Taser):0);
                if(ready)
                {
                    Vector3 shotMuzzle=action.FiredWeapon==action.Weapon?muzzle.position:Vec(action.FiredOrigin);
                    shotVisuals.Observe(seat,state.Round,action,now,shotMuzzle,practice);
                }
                if(ready)weaponAudio.Observe(seat,state.Round,state.Clock.Iteration,action,now,true,poseContext.Active&&(practice||state.Phase==DuelPhase.Playing),poseContext.Combat&&(practice||state.Alive(seat)),
                    gun.gameObject.activeSelf?gun.position:puppet.Root.TransformPoint(new Vector3(0,1.4f,-.45f)));
            }
            if(shared && holder<0)
            {
                var r=rigs[0,0];r.Gun.gameObject.SetActive(true);r.Gun.localPosition=new Vector3(0,1.035f,0);r.Gun.localRotation=Quaternion.Euler(0,140,90);
                r.Holding.gameObject.SetActive(false);r.Loading.gameObject.SetActive(false);r.Flash.SetActive(false);r.Light.intensity=0;
                r.Barrel.localRotation=Quaternion.identity;r.Latch.localRotation=Quaternion.identity;r.Hammer.localRotation=Quaternion.identity;
            }
            if (viewCamera != null)
            {
                Quaternion around = SeatRotation(localSeat) * Quaternion.Euler(0, 180, 0);
                var own=state.ForSeat(localSeat);
                float spectatorStart=SpectatorTransitionStart(own.LivesRemaining);
                bool restoringBeforeSpectating=ownDeath&&own.LivesRemaining==0&&now-own.DiedAt<spectatorStart+.4f;
                bool finalView=ready&&!ownPractice&&state.Phase==DuelPhase.Finished&&!restoringBeforeSpectating;
                bool fixedView=finalView||ready&&ownDeath&&own.LivesRemaining==0&&now-own.DiedAt>=spectatorStart+.4f;
                float overview = ownDeath ? finalView?1:Smooth((float)(now - state.ForSeat(localSeat).DiedAt) / 0.8f) : 0;
                Vector3 position = around * Vector3.Lerp(cameraHome, spectatorHome, overview);
                Quaternion rotation = Quaternion.LookRotation(new Vector3(0, 1.05f, 0) - position);
                bool aliveView = ready && (ownPractice||state.Phase==DuelPhase.Waiting ||
                    state.Alive(localSeat)&&state.Phase==DuelPhase.Playing&&(state.Stage==DuelStage.Countdown||state.Stage==DuelStage.Racing));
                if (aliveView && firstPerson != null)
                { position=Vec(DuelGunplay.Eye(localSeat)); rotation=firstPerson.Rotation(now); lastEye=position; lastEyeRotation=rotation; }
                bool faceFall = !finalView && ownDeath && (state.Mode == DuelMode.PvP||own.LivesRemaining==0);
                if (faceFall)
                {
                    float age = Mathf.Max(0, (float)(now - state.ForSeat(localSeat).DiedAt));
                    var head = puppets[localSeat].Head;
                    Vector3 eye = head.TransformPoint(new Vector3(0, 0.04f, -0.13f));
                    Quaternion eyeRotation = head.rotation * Quaternion.Euler(0, 180, 0);
                    float enter = Smooth(age / 0.12f);
                    float spectate = Smooth((age - spectatorStart) / 0.4f);
                    Vector3 home = lastEye;
                    Quaternion homeRotation = lastEyeRotation;
                    Vector3 spectatorPosition = around * spectatorHome;
                    position = Vector3.Lerp(Vector3.Lerp(home, eye, enter), spectatorPosition, spectate);
                    rotation = Quaternion.Slerp(Quaternion.Slerp(homeRotation, eyeRotation, enter),
                        Quaternion.LookRotation(new Vector3(0, 1.05f, 0) - spectatorPosition), spectate);
                }
                bool recovering=!finalView&&ownDeath&&own.LivesRemaining>0&&own.ReviveAt>=0&&now>=own.ReviveAt;
                if(recovering)
                {
                    float blend=Smooth((float)((now-own.ReviveAt)/.35));
                    var head=puppets[localSeat].Head;
                    position=Vector3.Lerp(position,head.TransformPoint(new Vector3(0,.04f,-.13f)),blend);
                    rotation=Quaternion.Slerp(rotation,head.rotation*Quaternion.Euler(0,180,0),blend);
                }
                if(ready&&!fixedView&&!aliveView&&!recovering&&firstPerson!=null)
                {
                    float freeLook=faceFall?Smooth((float)(now-own.DiedAt-spectatorStart)/.4f):1;
                    rotation=Quaternion.Slerp(rotation,firstPerson.OverviewRotation(rotation),freeLook);
                }
                if(watching)
                {position=Vec(DuelGunplay.Eye(viewSeat));rotation=spectator.Rotation(state,now);fixedView=true;faceFall=false;}
                // A watched player's head is hidden only on this spectator's camera.
                ApplyLocalVisibility(viewSeat,watching||(aliveView&&firstPerson!=null)||recovering,!watching&&(faceFall||recovering)&&Vector3.Distance(position,puppets[localSeat].Head.position)<.4f);

                viewCamera.nearClipPlane = faceFall ? 0.025f : 0.05f;
                float elapsed = Mathf.Max(0, (float)(now - lastShotAt));
                float shake = !fixedView && lastShotAt >= 0 ? Mathf.Exp(-elapsed * 13) * (HitPulse > 0 ? 0.06f : 0.02f) : 0;
                // Aim and sight must share one rotation, including server sway. Hit motion is confined to non-aim views.
                viewCamera.transform.position = position + (aliveView||watching ? Vector3.zero : new Vector3(Mathf.Sin(elapsed * 91), Mathf.Cos(elapsed * 73), 0) * shake);
                viewCamera.transform.rotation = aliveView||fixedView ? rotation : rotation * Quaternion.Euler(-HitPulse * 5, 0, HitPulse * 3);
                viewCamera.fieldOfView = watching ? viewAds?DuelGunplay.AdsFov(state.ForSeat(viewSeat).Weapon):DuelGunplay.BaseFov : aliveView ? firstPerson.Ads ? DuelGunplay.AdsFov(state.ForSeat(localSeat).Weapon) : DuelGunplay.BaseFov : 57;
                viewModelLayer?.Present();
                GetComponent<RouletteView>()?.SetViewModelTexture(viewModelLayer!=null&&viewModelLayer.Visible?viewModelLayer.Texture:null);
            }
        }
        private Puppet BuildPuppet(int seat, Vector3 position, Quaternion rotation)
        {
            Color[] shirts={new Color(.66f,.57f,.42f),new Color(.49f,.59f,.60f),new Color(.65f,.48f,.37f),new Color(.56f,.59f,.47f),new Color(.55f,.50f,.61f),new Color(.64f,.58f,.49f)};
            Color[] vests={new Color(.13f,.105f,.085f),new Color(.11f,.16f,.19f),new Color(.23f,.105f,.09f),new Color(.16f,.19f,.12f),new Color(.15f,.13f,.20f),new Color(.11f,.17f,.17f)};
            Color[] skins={new Color(.61f,.44f,.30f),new Color(.70f,.57f,.42f),new Color(.47f,.32f,.23f),new Color(.66f,.49f,.35f),new Color(.53f,.38f,.28f),new Color(.72f,.59f,.45f)};
            var shirt=Mat("Player "+seat+" woven shirt",shirts[seat],0,.12f);
            var vest=Mat("Player "+seat+" wool waistcoat",vests[seat],0,.09f);
            var skin=skinMaterials[seat]=Mat("Player "+seat+" skin",skins[seat],0,.16f);
            var trousers=Mat("Player "+seat+" trousers",Color.Lerp(vests[seat],new Color(.075f,.075f,.07f),.45f),0,.08f);
            var leather=Mat("Player "+seat+" leather",new Color(.065f,.049f,.036f),0,.24f);
            var hair=Mat("Player "+seat+" hair",seat==1?new Color(.21f,.13f,.07f):new Color(.07f,.055f,.045f),0,.1f);
            var trim=Mat("Player "+seat+" brass buttons",new Color(.45f,.33f,.15f),.5f,.32f);
            var p = new Puppet{Seat=seat}; p.Root = new GameObject("Player " + (seat+1)).transform; p.Root.SetParent(world, false);
            p.Root.localPosition = position; p.Root.localRotation = rotation;
            p.Torso = Faceted("Torso",p.Root,new Vector3(0,1.19f-DuelGunplay.SeatedDrop,0),Vector3.one,vest,
                new Vector4(-.36f,.25f,.17f,0),new Vector4(-.10f,.27f,.20f,0),new Vector4(.27f,.33f,.21f,.005f),new Vector4(.42f,.24f,.15f,.015f));
            Faceted("Shirt front",p.Torso,new Vector3(0,.10f,-.205f),Vector3.one,shirt,
                new Vector4(-.19f,.025f,.016f,0),new Vector4(.18f,.105f,.022f,.015f),new Vector4(.30f,.09f,.016f,.028f));
            // The sleeveless V-front follows the chest on both collapse and hit poses.
            foreach(float side in new[]{-1f,1f})
            {
                var panel=Faceted("Waistcoat panel",p.Torso,new Vector3(side*.13f,-.01f,-.155f),new Vector3(.25f,.56f,.095f),vest,
                    new Vector4(-.5f,.48f,.42f,0),new Vector4(.18f,.52f,.46f,0),new Vector4(.5f,.33f,.32f,.12f));
                panel.localRotation=Quaternion.Euler(0,side*-12,side*-8);
                var collar=Part("Shirt collar",PrimitiveType.Cube,p.Torso,new Vector3(side*.075f,.37f,-.175f),new Vector3(.11f,.12f,.045f),shirt);
                collar.localRotation=Quaternion.Euler(12,0,side*28);
                Part("Waistcoat pocket",PrimitiveType.Cube,p.Torso,new Vector3(side*.16f,-.16f,-.216f),new Vector3(.12f,.024f,.018f),leather);
            }
            for(int i=0;i<3;i++)Part("Brass waistcoat button",PrimitiveType.Cube,p.Torso,new Vector3(0,-.08f-i*.095f,-.215f),new Vector3(.022f,.025f,.014f),trim);
            var neck=Faceted("Neck",p.Root,new Vector3(0,1.67f-DuelGunplay.SeatedDrop,0),Vector3.one,skin,new Vector4(-.075f,.075f,.07f,0),new Vector4(.08f,.08f,.075f,0));
            p.Head = new GameObject("Head pivot").transform; p.Head.SetParent(p.Root, false); p.Head.localPosition = new Vector3(0, 1.87f-DuelGunplay.SeatedDrop, -0.04f);
            Faceted("Head",p.Head,Vector3.zero,Vector3.one,skin,
                new Vector4(-.215f,.075f,.075f,-.025f),new Vector4(-.13f,.135f,.13f,-.015f),new Vector4(.015f,.177f,.163f,0),new Vector4(.145f,.166f,.149f,.014f),new Vector4(.213f,.104f,.105f,.018f));
            // Keep the face unpainted and abstract, as in the portfolio concept art.
            foreach(float x in new[]{-.17f,.17f})Faceted("Ear",p.Head,new Vector3(x,-.018f,.015f),new Vector3(.04f,.09f,.055f),skin,new Vector4(-.5f,.35f,.34f,0),new Vector4(0,.55f,.5f,0),new Vector4(.5f,.30f,.3f,0));
            Faceted("Hair cap",p.Head,new Vector3(0,.105f,.016f),Vector3.one,hair,new Vector4(-.015f,.175f,.153f,0),new Vector4(.08f,.163f,.15f,0),new Vector4(.132f,.105f,.106f,0));
            if(seat==0||seat==3)
            {
                Faceted("Hat brim",p.Head,new Vector3(0,.16f,0),Vector3.one,vest,new Vector4(-.012f,.29f,.245f,0),new Vector4(.012f,.28f,.24f,0));
                var crown=Faceted("Hat crown",p.Head,new Vector3(0,.177f,0),Vector3.one,vest,new Vector4(0,.18f,.155f,0),new Vector4(.08f,.163f,.147f,0),new Vector4(.115f,.132f,.115f,.008f));
                Faceted("Hat band",p.Head,new Vector3(0,.185f,0),Vector3.one,leather,new Vector4(0,.182f,.157f,0),new Vector4(.027f,.176f,.155f,0));
                if(seat==3)crown.localScale=new Vector3(1.1f,.6f,1.2f);
            }
            if(seat==5)Faceted("Tied hair",p.Head,new Vector3(0,.11f,.15f),new Vector3(.15f,.15f,.14f),hair,new Vector4(-.5f,.2f,.23f,0),new Vector4(0,.52f,.5f,0),new Vector4(.5f,.25f,.26f,0));
            p.Upper = Sleeve("Right upper arm",p.Root,shirt);
            p.Forearm = Sleeve("Right forearm",p.Root,shirt);
            p.Hand = AvatarHand("Hand",p.Root,Vector3.zero,new Vector3(.15f,.10f,.20f),skin,false);
            p.LeftArm = Sleeve("Left upper arm",p.Root,shirt);
            p.LeftForearm = Sleeve("Left forearm",p.Root,shirt);
            p.LeftHand = AvatarHand("Resting hand",p.Root,Vector3.zero,new Vector3(.18f,.10f,.24f),skin,false,true);
            p.RightFingers=new Transform[4];p.LeftFingers=new Transform[4];
            for(int i=0;i<4;i++){p.RightFingers[i]=p.Hand.Find("Finger "+i);p.LeftFingers[i]=p.LeftHand.Find("Finger "+i);}
            foreach(var forearm in new[]{p.Forearm,p.LeftForearm})Faceted("Rolled cuff",forearm,new Vector3(0,.72f,0),Vector3.one,shirt,new Vector4(0,.53f,.53f,0),new Vector4(.22f,.49f,.49f,0));
            p.Body = new GameObject("Collapse pivot").transform; p.Body.SetParent(p.Root, false); p.Body.localPosition = new Vector3(0, 0.95f-DuelGunplay.SeatedDrop, 0);
            var children = new List<Transform>(); foreach (Transform child in p.Root) if (child != p.Body) children.Add(child);
            foreach (var child in children) child.SetParent(p.Body, true);
            Faceted("Seated pelvis",p.Root,new Vector3(0,.81f-DuelGunplay.SeatedDrop,0),Vector3.one,trousers,new Vector4(-.12f,.26f,.17f,0),new Vector4(.06f,.29f,.20f,0),new Vector4(.13f,.25f,.17f,0));
            foreach (float x in new[]{-.22f,.22f})
            {
                var thigh=Sleeve("Thigh",p.Root,trousers);
                Limb(thigh,p.Root.TransformPoint(new Vector3(x,.80f-DuelGunplay.SeatedDrop,0)),p.Root.TransformPoint(new Vector3(x,.67f-DuelGunplay.SeatedDrop,-.33f)),.22f);
                Faceted("Knee",p.Root,new Vector3(x,.66f-DuelGunplay.SeatedDrop,-.33f),new Vector3(.24f,.25f,.24f),trousers,new Vector4(-.5f,.30f,.33f,0),new Vector4(0,.53f,.54f,0),new Vector4(.5f,.30f,.33f,0));
                var shin=Sleeve("Shin",p.Root,trousers);
                Limb(shin,p.Root.TransformPoint(new Vector3(x,.65f-DuelGunplay.SeatedDrop,-.33f)),p.Root.TransformPoint(new Vector3(x,.16f,-.36f)),.18f);
                MeshPart("Shoe",shoeMesh,p.Root,new Vector3(x,.10f,-.40f),new Vector3(.22f,.17f,.32f),leather);
            }
            BuildNoruFace(p,seat);
            var headRenderers=new List<Renderer>(p.Head.GetComponentsInChildren<Renderer>());
            headRenderers.Add(neck.GetComponent<Renderer>());p.HeadRenderers=headRenderers.ToArray();
            p.Renderers=p.Root.GetComponentsInChildren<Renderer>(true);
            var armRenderers=new List<Renderer>();
            foreach(var bone in new[]{p.Upper,p.Forearm,p.Hand,p.LeftArm,p.LeftForearm,p.LeftHand})armRenderers.AddRange(bone.GetComponentsInChildren<Renderer>(true));
            p.ArmRenderers=armRenderers.ToArray();
            return p;
        }
        private void ApplyLocalVisibility(int localSeat,bool firstPersonView,bool hideFace)
        {
            for(int seat=0;seat<puppets.Length;seat++)
            {
                var p=puppets[seat];
                foreach(var renderer in p.Renderers)renderer.forceRenderingOff=false;
                foreach(var renderer in p.HeadRenderers)renderer.forceRenderingOff=(firstPersonView||hideFace)&&seat==localSeat;
            }
            drinkVisuals.SetLocalVisibility(localSeat,false);
        }
        private Mesh AvatarMesh(string name,params Vector4[] sections)
        {var mesh=DuelFacetedMesh.Sections(name,sections);meshes.Add(mesh);return mesh;}
        private Transform Faceted(string name,Transform parent,Vector3 position,Vector3 scale,Material material,params Vector4[] sections)
            =>MeshPart(name,AvatarMesh(name,sections),parent,position,scale,material);
        private Transform Sleeve(string name,Transform parent,Material material)
            =>MeshPart(name,sleeveMesh,parent,Vector3.zero,Vector3.one,material);
        private static Transform MeshPart(string name,Mesh mesh,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        private Transform AvatarHand(string name,Transform parent,Vector3 position,Vector3 scale,Material skin,bool gripping,bool left=false)
        {
            var palm=MeshPart(name,gripping?gripMesh:handMesh,parent,position,scale,skin);
            // Separate angular knuckles and thumb read clearly at first-person distance.
            for(int i=0;i<4;i++)MeshPart("Finger "+i,handMesh,palm,new Vector3((i-1.5f)*.22f,gripping?.08f:0,-.32f),new Vector3(.19f,.68f,gripping?.43f:.62f),skin);
            var thumb=MeshPart("Thumb",handMesh,palm,new Vector3(left?.43f:-.43f,-.06f,.05f),new Vector3(.28f,.60f,.48f),skin);thumb.localRotation=Quaternion.Euler(0,0,left?28:-28);
            return palm;
        }
        private void AnimatePuppet(Puppet p, float lift, float hit, float fall, float lookYaw, float lookPitch,float recovery)
            =>AnimatePuppetFrame(p,lift,hit,fall,lookYaw,lookPitch,recovery,new DuelPoseContext{Seat=p.Seat});
        private void AnimatePuppetFrame(Puppet p,float lift,float hit,float fall,float lookYaw,float lookPitch,float recovery,DuelPoseContext context)
        {
            // Faces point along local -Z; seated legs stay outside the upper-body pivot.
            float facingYaw=p.Root.eulerAngles.y+180;
            float turn=Mathf.DeltaAngle(facingYaw,lookYaw);
            float follow=1-Mathf.Exp(-Time.deltaTime*12);
            var targetPose=DuelCharacterPose.Sample(context);
            if(targetPose.RightChin>0||targetPose.LeftChin>0)
            {
                float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,55,Mathf.Max(Mathf.Abs(turn),Mathf.Abs(lookPitch)*1.5f)));
                targetPose.RightChin*=1-release;targetPose.LeftChin*=1-release;
            }
            p.Look.Step(turn,lookPitch,Time.deltaTime);
            p.Pose=p.PoseInitialized?DuelCharacterPose.Blend(p.Pose,targetPose,1-Mathf.Exp(-Time.deltaTime*9)):targetPose;p.PoseInitialized=true;
            var pose=p.Pose;
            // Suppress idle body motion during combat while retaining hit reactions.
            if(context.Combat&&fall<.001f&&recovery<=0)
            {pose.Breath=0;if(!context.Active){pose.Lean=pose.Roll=pose.HeadLift=0;}}
            float looking=1-fall;
            p.Body.localRotation = Quaternion.Euler(-82 * fall+(pose.Lean+hit*6)*looking, 0, (pose.Roll+hit*2)*looking);
            // Rotate around the waist seam, keeping the seated pelvis in the chair.
            // A forward translation here separates the jacket from the hips.
            var waist=new Vector3(0,-.12f,0);
            var seated=new Vector3(0,.83f-DuelGunplay.SeatedDrop+pose.Breath,0)-p.Body.localRotation*waist;
            p.Body.localPosition=Vector3.Lerp(seated,new Vector3(0,1.04f,-.12f),fall);
            p.Head.localPosition = new Vector3(0, 0.92f, Mathf.Lerp(-0.04f, -0.10f, fall));
            if(context.Combat&&fall<.001f&&recovery<=0&&hit<=0)
                p.Head.position=Vec(DuelGunplay.Point(p.Seat,DuelHitPart.Head));
            // Compose the gaze in seat space so a leaning/rolling torso cannot tilt
            // the neck's yaw axis or reverse the player's vertical look.
            var gaze=p.Root.rotation*Quaternion.Euler(p.Look.Pitch+pose.HeadLift+hit*3,p.Look.Yaw,p.Look.Roll+hit*2);
            p.Head.localRotation=Quaternion.Slerp(Quaternion.Euler(hit*3,0,hit*2),Quaternion.Inverse(p.Body.rotation)*gaze,looking);
            p.Torso.localRotation = Quaternion.identity;
            Vector3 shoulder = p.Body.TransformPoint(new Vector3(-0.28f, 0.53f, 0));
            Vector3 leftShoulder=p.Body.TransformPoint(new Vector3(.28f,.53f,0));
            bool attached=holdingHand.gameObject.activeSelf&&fall<.001f;
            bool leftAttached=loadingHand.gameObject.activeSelf&&fall<.001f;
            bool rifle=context.Weapon==DuelWeapon.BAS;
            bool rifleSupport=rifle&&rigs[p.Seat,3].Support.gameObject.activeSelf;
            Vector3 support=rifle?BasSupportWrist:context.Weapon==DuelWeapon.SawedOff?new Vector3(0,-.13f,.30f):new Vector3(-.07f,-.22f,-.14f);
            if(attached&&!context.ViewModel)FitHeldGun(shoulder,leftShoulder,support,leftAttached,pose.Support>.05f);
            Vector3 resting = IdlePoint(p,pose.Right);
            drinkVisuals.Targets(p.Seat,context.DrinkAction,context.DrinkProgress,p.Root,p.Head,out var drinkRight,out var drinkLeft,out var drinkRightRotation,out var drinkLeftRotation);
            Vector3 hand = Vector3.Lerp(resting,p.Body.TransformPoint(new Vector3(-.42f,.6f,-.12f)),fall);
            if(attached)hand=holdingHand.position;
            else if(context.Active&&context.Action==DuelAction.Pickup&&fall<.001f)
                hand=Vector3.Lerp(hand,holdingHand.position,Smooth(context.ActionAge/.4f)*.85f);
            if(p.HandsInitialized&&!attached)hand=Vector3.Lerp(p.RightPosition,hand,follow);
            float brace=Smooth(recovery/.12f)*(1-Smooth((recovery-.72f)/.28f));
            hand=Vector3.Lerp(hand,p.Root.TransformPoint(DuelArmIK.RightBrace),brace);
            if(!attached&&fall<.001f)hand=Vector3.Lerp(hand,drinkRight,drinkVisuals.RightGrip(p.Seat));
            if(context.Ink&&fall<.001f)hand=InkHandPoint(p.Seat,lookYaw,lookPitch,context.InkAge);
            Vector3 hint=Vector3.Lerp((shoulder+hand)*.5f+p.Body.TransformDirection(new Vector3(-.23f,-.15f,.08f)),IdlePoint(p,pose.RightElbow),pose.Relaxed*looking*(1-brace));
            ChinSupport(p,shoulder,true,pose.RightChin*looking*(1-brace),ref hand,ref hint);
            DuelArmIK.Solve(shoulder,hand,hint,out var elbow,out hand);
            bool viewRight=context.ViewModel&&attached;
            var viewFacing=Quaternion.Euler(-lookPitch,lookYaw,0);
            if(viewRight){hand=holdingHand.position;elbow=ViewModelElbow(hand,true,viewFacing);shoulder=ViewModelShoulder(elbow,true,viewFacing);}
            ArmLimb(p.Upper,shoulder,elbow,viewRight?ViewUpperLength:DuelArmIK.Upper,.15f,viewRight?viewFacing:p.Body.rotation);ArmLimb(p.Forearm,elbow,hand,viewRight?ViewForearmLength:DuelArmIK.Forearm,.13f,viewRight?viewFacing:p.Body.rotation);
            p.Upper.GetComponent<Renderer>().enabled=true;
            p.Hand.position = hand;
            var rightRotation=attached?holdingHand.rotation:p.Body.rotation*Quaternion.Euler(pose.RightEuler);
            rightRotation=Quaternion.Slerp(rightRotation,drinkRightRotation,drinkVisuals.RightGrip(p.Seat));
            p.Hand.rotation = viewRight?rightRotation:Quaternion.Slerp(p.Hand.rotation,rightRotation,follow);
            if(context.Ink&&fall<.001f)p.Hand.rotation=Quaternion.Euler(-lookPitch,lookYaw,0)*Quaternion.Euler(0,85,0);
            p.Hand.rotation=Quaternion.Slerp(p.Hand.rotation,p.Root.rotation,brace);
            p.Hand.gameObject.SetActive(!holdingHand.gameObject.activeSelf);
            Vector3 leftRest = Vector3.Lerp(IdlePoint(p,pose.Left),p.Body.TransformPoint(new Vector3(.42f,.6f,-.12f)),fall);
            Vector3 leftHand = leftAttached?loadingHand.position:rifleSupport?rigs[p.Seat,3].Support.position:Vector3.Lerp(leftRest,gun.TransformPoint(support),pose.Support*looking);
            bool viewLeft=context.ViewModel&&(leftAttached||rifleSupport||pose.Support>.05f);
            if(p.HandsInitialized&&!leftAttached&&!viewLeft&&!rifleSupport)leftHand=Vector3.Lerp(p.LeftPosition,leftHand,follow);
            leftHand=Vector3.Lerp(leftHand,p.Root.TransformPoint(DuelArmIK.LeftBrace),brace);
            if(!leftAttached&&fall<.001f)leftHand=Vector3.Lerp(leftHand,drinkLeft,drinkVisuals.LeftGrip(p.Seat));
            Vector3 leftHint=Vector3.Lerp((leftShoulder+leftHand)*.5f+p.Body.TransformDirection(new Vector3(.23f,-.15f,.08f)),IdlePoint(p,pose.LeftElbow),pose.Relaxed*looking*(1-brace));
            ChinSupport(p,leftShoulder,false,pose.LeftChin*looking*(1-brace),ref leftHand,ref leftHint);
            var leftDesired=leftHand;
            DuelArmIK.Solve(leftShoulder,leftHand,leftHint,out var leftElbow,out leftHand);
            if(viewLeft){leftHand=leftDesired;leftElbow=ViewModelElbow(leftHand,false,viewFacing);leftShoulder=ViewModelShoulder(leftElbow,false,viewFacing);}
            ArmLimb(p.LeftArm,leftShoulder,leftElbow,viewLeft?ViewUpperLength:DuelArmIK.Upper,.16f,viewLeft?viewFacing:p.Body.rotation);
            ArmLimb(p.LeftForearm,leftElbow,leftHand,viewLeft?ViewForearmLength:DuelArmIK.Forearm,.13f,viewLeft?viewFacing:p.Body.rotation);
            p.LeftArm.GetComponent<Renderer>().enabled=true;
            p.LeftHand.position = leftHand;
            var leftRotation=leftAttached?loadingHand.rotation:Quaternion.Slerp(p.Body.rotation*Quaternion.Euler(pose.LeftEuler),gun.rotation*Quaternion.Euler(-12,70,0),pose.Support*looking);
            leftRotation=Quaternion.Slerp(leftRotation,drinkLeftRotation,drinkVisuals.LeftGrip(p.Seat));
            p.LeftHand.rotation = viewLeft?leftRotation:Quaternion.Slerp(p.LeftHand.rotation,leftRotation,follow);
            p.LeftHand.rotation=Quaternion.Slerp(p.LeftHand.rotation,p.Root.rotation,brace);
            p.LeftHand.gameObject.SetActive(!loadingHand.gameObject.activeSelf&&!rifleSupport);
            p.RightPosition=hand;p.LeftPosition=leftHand;p.HandsInitialized=true;
            drinkVisuals.AlignHeld(p.Seat,p.Hand,p.LeftHand,context.DrinkAction,context.DrinkProgress);
            float drum=pose.Drum*looking*(1-brace);float time=(float)(context.Time%10000);
            for(int i=0;i<4;i++)
            {
                p.RightFingers[i].localRotation=Quaternion.Euler(-pose.RightCurl*looking*(1-brace)+Mathf.Max(0,Mathf.Sin(time*3.2f+i*.8f))*12*drum,0,0);
                p.LeftFingers[i].localRotation=Quaternion.Euler(-pose.LeftCurl*looking*(1-brace),0,0);
            }
        }
        private static Vector3 IdlePoint(Puppet p,Vector3 point)
        {
            var worldPoint=p.Body.TransformPoint(point);
            float tableWeight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.25f,point.y));
            worldPoint.y=Mathf.Lerp(worldPoint.y,p.Root.position.y+.915f+point.y,tableWeight);
            return worldPoint;
        }
        private static void ChinSupport(Puppet p,Vector3 shoulder,bool right,float weight,ref Vector3 wrist,ref Vector3 hint)
        {
            if(weight<=0)return;
            var target=p.Head.TransformPoint(new Vector3(right?-.055f:.055f,-.27f,-.04f));
            var localShoulder=p.Root.InverseTransformPoint(shoulder);var localTarget=p.Root.InverseTransformPoint(target);
            if(!DuelArmIK.TableElbow(localShoulder,localTarget,DuelArmIK.TableElbowY,new Vector3(right?-.30f:.30f,DuelArmIK.TableElbowY,-.72f),out var elbow))return;
            wrist=Vector3.Lerp(wrist,target,weight);hint=Vector3.Lerp(hint,p.Root.TransformPoint(elbow),weight);
        }
        private void FitHeldGun(Vector3 rightShoulder,Vector3 leftShoulder,Vector3 support,bool loading,bool supporting)
        {
            // Keep third-person props inside the intersection of the arms' reach volumes.
            // First-person sights use their camera placement instead; damage is never derived here.
            for(int i=0;i<12;i++)
            {
                Vector3 delta=DuelArmIK.Limit(rightShoulder,holdingHand.position)-holdingHand.position;
                float correction=delta.sqrMagnitude;
                gun.position+=delta;if(loading)loadingHand.position+=delta;
                if(loading||supporting)
                {
                    Vector3 target=loading?loadingHand.position:gun.TransformPoint(support);
                    delta=DuelArmIK.Limit(leftShoulder,target)-target;
                    correction+=delta.sqrMagnitude;
                    gun.position+=delta;if(loading)loadingHand.position+=delta;
                }
                if(correction<.0000000001f)break;
            }
        }
        private const float ViewForearmLength=.50f,ViewUpperLength=.40f;
        private static Vector3 ViewModelElbow(Vector3 wrist,bool right,Quaternion facing)
        {
            var direction=new Vector3(right?.28f:-.28f,-.40f,-.80f).normalized;
            return wrist+facing*direction*ViewForearmLength;
        }
        private static Vector3 ViewModelShoulder(Vector3 elbow,bool right,Quaternion facing)=>
            elbow+facing*new Vector3(right?.18f:-.18f,-.28f,-1).normalized*ViewUpperLength;
        private static Quaternion ArmRotation(Vector3 direction,Quaternion facing)
        {
            var up=direction.normalized;
            var forward=Vector3.ProjectOnPlane(facing*Vector3.forward,up);
            if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(facing*Vector3.right,up);
            return Quaternion.LookRotation(forward.normalized,up);
        }
        private static void ArmLimb(Transform limb,Vector3 from,Vector3 to,float length,float width,Quaternion facing)
        {
            limb.position=(from+to)*.5f;limb.rotation=ArmRotation(to-from,facing);
            limb.localScale=new Vector3(width,length*.5f,width);
        }
        private static void Limb(Transform limb, Vector3 from, Vector3 to, float width)
        {
            limb.position = (from + to) / 2; limb.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
            limb.localScale = new Vector3(width, Vector3.Distance(from, to) / 2, width);
        }
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        private static Vector3 Vec(AimVector a) => new Vector3(a.X,a.Y,a.Z);
        private Material Mat(string name, Color color, float metallic, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smoothness);
            materials.Add(material); return material;
        }
        private static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>()); return go.transform;
        }
        private static Transform Axial(string name, Transform parent, Vector3 position, float diameter, float length, Material material)
        {
            var part = Part(name, PrimitiveType.Cylinder, parent, position, new Vector3(diameter, length / 2, diameter), material);
            part.localRotation = Quaternion.Euler(90, 0, 0);
            return part;
        }
        private void Ring(string name, Transform parent, Vector3 position, float radius, float tube, Material material, Quaternion rotation)
        {
            const int segments = 40, sides = 8;
            var vertices = new Vector3[segments * sides]; var indices = new int[segments * sides * 6];
            for (int i = 0; i < segments; i++) for (int j = 0; j < sides; j++)
            {
                float a = i * Mathf.PI * 2 / segments, b = j * Mathf.PI * 2 / sides;
                vertices[i * sides + j] = new Vector3((radius + tube * Mathf.Cos(b)) * Mathf.Cos(a), (radius + tube * Mathf.Cos(b)) * Mathf.Sin(a), tube * Mathf.Sin(b));
                int p = (i * sides + j) * 6, a0 = i * sides + j, b0 = ((i + 1) % segments) * sides + j;
                int c = ((i + 1) % segments) * sides + (j + 1) % sides, d = i * sides + (j + 1) % sides;
                indices[p] = a0; indices[p + 1] = b0; indices[p + 2] = c; indices[p + 3] = a0; indices[p + 4] = c; indices[p + 5] = d;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = indices }; mesh.RecalculateNormals(); meshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = rotation;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        private void LightAt(string name, Vector3 position, Color color, float intensity, float range, bool shadows)
        {
            var go = new GameObject(name); go.transform.SetParent(world, false); go.transform.localPosition = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }
        private void OnDestroy()
        {
            viewModelLayer?.Dispose();
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (world != null) Destroy(world.gameObject);
        }
    }
}



