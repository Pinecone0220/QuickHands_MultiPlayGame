using UnityEngine;
using UnityEngine.Rendering;

namespace HelloWorld
{
    // Local cosmetic flight from the rendered muzzle to the server's ray endpoint.
    // No physics, colliders, damage callbacks or replicated projectile objects.
    public sealed class DuelShotVisuals : MonoBehaviour
    {
        private sealed class Flight
        {
            public LineRenderer Line;
            public Vector3 From, To;
            public float Born, Distance;
        }
        private readonly Flight[,] flights=new Flight[RouletteRules.MaxPlayers,DuelGunplay.ShotgunPellets];
        private readonly double[] seenAt=new double[RouletteRules.MaxPlayers];
        private readonly int[] seenLife=new int[RouletteRules.MaxPlayers];
        private int round=-1;
        private Material material;
        public int ActiveCount { get; private set; }
        private static Vector3 Vec(AimVector v)=>new Vector3(v.X,v.Y,v.Z);
        public void Observe(int seat,int currentRound,DuelActionState action,double serverNow,Vector3 muzzle)
        {
            if(round!=currentRound){Clear();round=currentRound;}
            if(seenLife[seat]!=action.Life){Hide(seat);seenLife[seat]=action.Life;seenAt[seat]=-1;}
            if(action.FiredAt<0 || action.FiredAt==seenAt[seat])return;
            seenAt[seat]=action.FiredAt;
            // A late join or a stale snapshot must not replay historical shots.
            if(serverNow-action.FiredAt>.35 || serverNow-action.FiredAt<-.05)return;
            var centre=DuelGunplay.Direction(action.FiredYaw,action.FiredPitch);
            int count=action.FiredWeapon==DuelWeapon.SawedOff?DuelGunplay.ShotgunPellets:1;
            Hide(seat);
            for(int i=0;i<count;i++)
            {
                var ray=count==1?centre:DuelGunplay.PelletDirection(centre,action.FiredSpread,i,action.FiredSeed);
                var hit=DuelGunplay.TraceFrom(seat,action.FiredAliveMask,action.FiredOrigin,ray,new DuelTabletopState{BottlesBrokenMask=action.FiredBottleMask,GlassesBrokenMask=action.FiredGlassMask});
                var end=Vec(action.FiredOrigin+ray*Mathf.Min(hit.Distance,DuelGunplay.ShotRange(action.FiredWeapon,action.FiredAds)));
                var f=flights[seat,i]??(flights[seat,i]=Create(seat,i));
                f.From=muzzle;f.To=end;f.Distance=Vector3.Distance(muzzle,end);f.Born=Time.unscaledTime;
                f.Line.startColor=f.Line.endColor=action.FiredWeapon==DuelWeapon.Taser?new Color(.25f,.8f,1,1):Color.white;f.Line.startWidth=action.FiredWeapon==DuelWeapon.Taser?.015f:.007f;f.Line.enabled=true;Render(f,0);
            }
        }
        private Flight Create(int seat,int pellet)
        {
            if(material==null)
            {
                material=new Material(Resources.Load<Shader>("DuelBullet")){name="Cosmetic bullet light"};
                material.SetColor("_BaseColor",new Color(1f,.79f,.35f,1));
            }
            var go=new GameObject($"Visual bullet {seat+1}/{pellet+1}");go.transform.SetParent(transform,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;
            line.positionCount=2;line.startWidth=.007f;line.endWidth=.025f;line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            return new Flight{Line=line};
        }
        private static void Render(Flight f,float elapsed)
        {
            float head=Mathf.Min(f.Distance,.08f+elapsed*DuelGunplay.ProjectileSpeed);
            var direction=(f.To-f.From).normalized;
            f.Line.SetPosition(0,f.From+direction*Mathf.Max(0,head-.22f));
            f.Line.SetPosition(1,f.From+direction*head);
            if(elapsed*DuelGunplay.ProjectileSpeed>f.Distance+.22f)f.Line.enabled=false;
        }
        private void LateUpdate()
        {
            ActiveCount=0;
            foreach(var f in flights)if(f!=null&&f.Line.enabled){Render(f,Time.unscaledTime-f.Born);if(f.Line.enabled)ActiveCount++;}
        }
        private void Hide(int seat)
        {for(int i=0;i<DuelGunplay.ShotgunPellets;i++)if(flights[seat,i]!=null)flights[seat,i].Line.enabled=false;}
        public void Clear()
        {for(int seat=0;seat<RouletteRules.MaxPlayers;seat++){Hide(seat);seenAt[seat]=-1;seenLife[seat]=-1;}ActiveCount=0;}
        private void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
