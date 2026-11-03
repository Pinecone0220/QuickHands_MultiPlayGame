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
            public LineRenderer Line, Halo;
            public LineRenderer[] Filaments;
            public Vector3 From, To;
            public float Born, Distance;
            public bool HitscanTrail;public int SkinTier,Skin;public Color Tint;
        }
        private readonly Flight[,] flights=new Flight[RouletteRules.MaxPlayers,DuelGunplay.ShotgunPellets];
        private readonly double[] seenAt=new double[RouletteRules.MaxPlayers];
        private readonly int[] seenLife=new int[RouletteRules.MaxPlayers];
        private int round=-1;
        private Material material,rareMaterial;
        private Material electricMaterial;
        private readonly DuelTaserArc[] tasers=new DuelTaserArc[RouletteRules.MaxPlayers];
        public int TaserActiveCount {get;private set;}
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
            if(action.FiredWeapon==DuelWeapon.Taser)
            {
                Hide(seat);
                var hit=DuelGunplay.TraceHitscan(seat,action.FiredAliveMask,action.FiredOrigin,centre,new DuelTabletopState{BottlesBrokenMask=action.FiredBottleMask,GlassesBrokenMask=action.FiredGlassMask});
                if(electricMaterial==null){electricMaterial=new Material(Resources.Load<Shader>("DuelShotElectric")){name="Taser electric glow"};electricMaterial.SetColor("_BaseColor",new Color(2.2f,2.2f,2.2f,1));}
                var arc=tasers[seat]??(tasers[seat]=new DuelTaserArc(transform,electricMaterial,seat));
                arc.Play(muzzle,Vec(action.FiredOrigin+centre*hit.Distance),hit.Seat>=0||hit.Prop!=DuelTableProp.None||hit.Distance<DuelGunplay.TaserRange-.001f,action.ShotSerial*31+seat,DuelSkinEffects.Skin(seat,DuelItem.Taser));
                return;
            }
            int count=action.FiredWeapon==DuelWeapon.SawedOff?DuelGunplay.ShotgunPellets:1;
            Hide(seat);
            for(int i=0;i<count;i++)
            {
                var ray=count==1?centre:DuelGunplay.PelletDirection(centre,action.FiredSpread,i,action.FiredSeed);
                var props=new DuelTabletopState{BottlesBrokenMask=action.FiredBottleMask,GlassesBrokenMask=action.FiredGlassMask};
                var hit=action.FiredWeapon==DuelWeapon.BAS?DuelGunplay.TraceRifleHitscan(seat,action.FiredAliveMask,action.FiredOrigin,ray,props):DuelGunplay.TraceFrom(seat,action.FiredAliveMask,action.FiredOrigin,ray,props);
                var end=Vec(action.FiredOrigin+ray*Mathf.Min(hit.Distance,DuelGunplay.ShotRange(action.FiredWeapon,action.FiredAds)));
                var f=flights[seat,i]??(flights[seat,i]=Create(seat,i));
                f.From=muzzle;f.To=end;f.Distance=Vector3.Distance(muzzle,end);f.Born=Time.unscaledTime;f.HitscanTrail=action.FiredWeapon==DuelWeapon.BAS;
                int skin=DuelSkinEffects.WeaponSkin(seat,action.FiredWeapon);f.Skin=skin;f.SkinTier=DuelSkinEffects.WeaponTier(skin);f.Tint=DuelSkinEffects.WeaponGlow(skin,Time.unscaledTime);
                if(f.SkinTier>0&&rareMaterial==null){rareMaterial=new Material(Resources.Load<Shader>("DuelShotBullet")){name="Skin-colored bullet light"};rareMaterial.SetColor("_BaseColor",Color.white);}
                f.Line.sharedMaterial=f.SkinTier>0?rareMaterial:material;
                if(f.SkinTier>=3&&f.Halo==null)
                {
                    var glow=new GameObject("Rare bullet glow");glow.transform.SetParent(f.Line.transform,false);
                    f.Halo=glow.AddComponent<LineRenderer>();f.Halo.useWorldSpace=true;f.Halo.positionCount=2;f.Halo.numCapVertices=3;
                    f.Halo.shadowCastingMode=ShadowCastingMode.Off;f.Halo.receiveShadows=false;
                }
                if(f.Halo!=null){f.Halo.enabled=f.SkinTier>=3;f.Halo.sharedMaterial=rareMaterial;f.Halo.startWidth=f.Halo.endWidth=f.SkinTier==4?.055f:.038f;}
                if(i==0&&f.SkinTier>=2&&f.Filaments==null)
                {
                    f.Filaments=new LineRenderer[2];
                    for(int j=0;j<f.Filaments.Length;j++)
                    {
                        var ornament=new GameObject("Bullet trail filament "+j);ornament.transform.SetParent(f.Line.transform,false);
                        var line=ornament.AddComponent<LineRenderer>();line.sharedMaterial=rareMaterial;line.useWorldSpace=true;line.positionCount=24;
                        line.numCapVertices=2;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;f.Filaments[j]=line;
                    }
                }
                if(f.Filaments!=null)foreach(var filament in f.Filaments)filament.enabled=false;
                f.Line.startColor=f.Line.endColor=Color.white;f.Line.startWidth=f.SkinTier>0?.018f:f.HitscanTrail?.013f:.007f;f.Line.endWidth=f.SkinTier>0?.012f:f.HitscanTrail?.006f:.025f;f.Line.enabled=true;Render(f,0);
            }
        }
        private Flight Create(int seat,int pellet)
        {
            if(material==null)
            {
                material=new Material(Resources.Load<Shader>("DuelShotBullet")){name="Cosmetic bullet light"};
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
            if(f.SkinTier>0)f.Tint=DuelSkinEffects.WeaponGlow(f.Skin,Time.unscaledTime);
            if(f.HitscanTrail)
            {
                const float duration=.30f;float alpha=Mathf.Clamp01(1-elapsed/duration);
                f.Line.SetPosition(0,f.From);f.Line.SetPosition(1,f.To);
                Color tint=f.SkinTier>0?f.Tint:new Color(1,.94f,.8f);f.Line.startColor=new Color(tint.r,tint.g,tint.b,alpha*.8f);f.Line.endColor=new Color(tint.r,tint.g,tint.b,alpha*.35f);
                if(elapsed>=duration)f.Line.enabled=false;UpdateHalo(f);UpdateFilaments(f,elapsed,alpha);return;
            }
            if(f.SkinTier>0){f.Line.startColor=f.Line.endColor=f.Tint;}
            float head=Mathf.Min(f.Distance,.08f+elapsed*DuelGunplay.ProjectileSpeed);
            var direction=(f.To-f.From).normalized;
            f.Line.SetPosition(0,f.From+direction*Mathf.Max(0,head-(f.SkinTier>0?.65f:.22f)));
            f.Line.SetPosition(1,f.From+direction*head);
            if(elapsed*DuelGunplay.ProjectileSpeed>f.Distance+.22f)f.Line.enabled=false;UpdateHalo(f);UpdateFilaments(f,elapsed,1);
        }
        private static void UpdateFilaments(Flight f,float age,float fade)
        {
            if(f.Filaments==null)return;
            Vector3 start=f.Line.GetPosition(0),end=f.Line.GetPosition(1),direction=(end-start).normalized;
            if(direction.sqrMagnitude<.001f)direction=(f.To-f.From).normalized;
            Vector3 right=Vector3.Cross(direction,Mathf.Abs(direction.y)>.95f?Vector3.forward:Vector3.up).normalized;
            Vector3 up=Vector3.Cross(right,direction).normalized;
            for(int strand=0;strand<f.Filaments.Length;strand++)
            {
                var line=f.Filaments[strand];bool active=f.Line.enabled&&f.SkinTier>=2&&(strand==0||f.SkinTier>=3);
                line.enabled=active;if(!active)continue;
                line.startWidth=line.endWidth=f.SkinTier==4?.010f:f.SkinTier==3?.008f:.006f;
                Color tint=Color.Lerp(f.Tint,Color.white,f.SkinTier==4?.60f:.35f);tint.a=fade*(f.SkinTier==4?.85f:.65f);line.startColor=line.endColor=tint;
                float radius=f.SkinTier==4?.055f:f.SkinTier==3?.035f:.018f;
                float turns=f.SkinTier==4?4:f.SkinTier==3?3:1;
                for(int i=0;i<24;i++)
                {
                    float t=i/23f,angle=t*turns*Mathf.PI*2+age*9+strand*Mathf.PI;
                    float envelope=Mathf.Sin(t*Mathf.PI)*radius;
                    line.SetPosition(i,Vector3.Lerp(start,end,t)+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*envelope);
                }
            }
        }
        private static void UpdateHalo(Flight f)
        {
            if(f.Halo==null)return;
            f.Halo.enabled=f.Line.enabled&&f.SkinTier>=3;
            if(!f.Halo.enabled)return;
            f.Halo.SetPosition(0,f.Line.GetPosition(0));f.Halo.SetPosition(1,f.Line.GetPosition(1));
            Color glow=f.Tint;glow.a=f.Line.startColor.a*(f.SkinTier==4?.48f:.32f);f.Halo.startColor=f.Halo.endColor=glow;
        }
        private void LateUpdate()
        {
            ActiveCount=TaserActiveCount=0;
            foreach(var f in flights)if(f!=null&&f.Line.enabled){Render(f,Time.unscaledTime-f.Born);if(f.Line.enabled)ActiveCount++;}
            foreach(var arc in tasers)if(arc!=null){arc.Step(Time.unscaledTime);if(arc.Visible){TaserActiveCount++;ActiveCount++;}}
        }
        private void Hide(int seat)
        {for(int i=0;i<DuelGunplay.ShotgunPellets;i++)if(flights[seat,i]!=null){flights[seat,i].Line.enabled=false;if(flights[seat,i].Halo!=null)flights[seat,i].Halo.enabled=false;if(flights[seat,i].Filaments!=null)foreach(var line in flights[seat,i].Filaments)line.enabled=false;}tasers[seat]?.Hide();}
        public void Clear()
        {for(int seat=0;seat<RouletteRules.MaxPlayers;seat++){Hide(seat);seenAt[seat]=-1;seenLife[seat]=-1;}ActiveCount=TaserActiveCount=0;}
        private void OnDisable()=>Clear();
        private void OnDestroy(){if(material!=null)Destroy(material);if(rareMaterial!=null)Destroy(rareMaterial);if(electricMaterial!=null)Destroy(electricMaterial);}
    }
}
