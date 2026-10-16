using System;
using HelloWorld;
public static class GunplayRulesTests
{
    static int checks;
    static void Check(bool value,string text) { checks++; if(!value)throw new Exception(text); }
    sealed class PreciseRandom : Random
    { public override int Next(int min,int max)=>min==0&&max==PartyGames.Count?1:min; public override int Next(int max)=>0; public override double NextDouble()=>0; }
    sealed class QuickRandom:Random
    {public QuickRandom(int seed):base(seed){} public override int Next(int min,int max)=>min==0?max==PartyGames.Count?1:0:base.Next(min,max);}
    static RouletteRules Game(int count=6)
    { var g=new RouletteRules(DuelMode.PvP);for(int i=0;i<count;i++)g.Join(i);for(int i=0;i<count;i++)g.SetReady(i);Check(g.Start(),"all ready starts");return g; }
    static DuelDirector Director(DuelWeapon weapon=DuelWeapon.Revolver,float shotLimit=4)
    { var d=new DuelDirector(DuelMode.PvP,new PreciseRandom());for(int i=0;i<6;i++)d.Join(i,-16);d.Game.SetShotTime(shotLimit);if(weapon!=DuelWeapon.Revolver)d.SelectWeapon(0,weapon,-16);for(int i=0;i<6;i++)d.Ready(i,-16);d.Tick(-13.5);d.Tick(-7.69);d.Tick(-2.68);d.Tick(.33);return d; }
    static void Load(DuelDirector d,int seat,double now)
    {
        int n=0;
        while(d.ForSeat(seat).Action!=DuelAction.Aim && n++<50)
        {var a=d.ForSeat(seat);Check(d.Input(seat,a.Digit(a.Progress),a.Version,now),"load input");}
        Check(d.ForSeat(seat).Action==DuelAction.Aim,"loaded");
    }
    static bool FireAt(DuelDirector d,int seat,int target,DuelHitPart part,double now)
    {
        DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(seat),out float y,out float p);
        DuelGunplay.Sway(seat,d.ForSeat(seat).Weapon,true,now,now,out float sy,out float sp);
        bool fired=d.Fire(seat,y-sy,p-sp,true,d.ForSeat(seat).Version,now,now);d.Tick(now+.3);return fired;
    }
    static void PrepareRevolver(DuelDirector d,int rounds,double now)
    {
        while(d.ForSeat(0).Action!=DuelAction.Load){var a=d.ForSeat(0);Check(d.Input(0,a.Digit(a.Progress),a.Version,now),"six-digit revolver preparation");}
        Check(d.ForSeat(0).CorrectInputs==30&&d.ForSeat(0).Length==3,"five actions retained before three-digit loading");
        for(int r=0;r<rounds;r++)for(int n=0;n<3;n++){var a=d.ForSeat(0);Check(d.Input(0,a.Digit(a.Progress),a.Version,now),"insert a cartridge after three digits");}
    }
    static void ValidateRevolver()
    {
        for(int rounds=1;rounds<=6;rounds++)
        {
            var d=Director();Check(d.Look(0,0,0,true,3)&&d.ForSeat(0).Aiming&&d.ForSeat(0).Target==-1,"empty revolver can aim without warning");
            Check(!d.Fire(0,0,0,true,d.ForSeat(0).Version,3,3)&&d.Game.Shots==0,"empty ADS cannot fire");
            PrepareRevolver(d,rounds,4);var prepared=d.ForSeat(0);var peer=d.ForSeat(1);
            Check(prepared.Ammo==rounds&&prepared.CorrectInputs==30+rounds*3,"exact cartridge count and input budget");
            Check(prepared.Action==(rounds==6?DuelAction.Aim:DuelAction.Load),"partial loading can continue; full cylinder closes");
            if(rounds==6)Check(!d.Input(0,1,prepared.Version,4),"cannot load seventh cartridge");
            for(int shot=0;shot<rounds;shot++)
            {
                double at=5+shot;var a=d.ForSeat(0);
                Check(d.Fire(0,0,65,true,a.Version,at,at),"loaded partial or full cylinder fires by mouse");
                Check(!d.Fire(0,0,65,true,a.Version,at,at),"duplicate cannot consume a second cartridge");
                d.Tick(at+.6);
                Check(d.ForSeat(0).Ammo==rounds-shot-1&&d.ForSeat(0).CorrectInputs==prepared.CorrectInputs,"remaining rounds fire without extra commands");
                Check(d.ForSeat(1).Equals(peer),"revolver shots never alter another action");
            }
            Check(d.ForSeat(0).Action==DuelAction.CloseBarrel&&!d.Fire(0,0,65,true,d.ForSeat(0).Version,12,12),"empty cylinder starts spent-case ejection and cannot fire");
            Check(RouletteRules.Count(d.ForSeat(0).SpentMask)==rounds,"fired cases retained until ejection");
            int before=d.ForSeat(0).CorrectInputs;Load(d,0,12);
            Check(d.ForSeat(0).CorrectInputs-before==24&&d.ForSeat(0).Ammo==6&&d.ForSeat(0).SpentMask==0,"subsequent cylinder needs only six-digit ejection and six three-digit rounds");
        }
        var refill=Director();PrepareRevolver(refill,6,4);
        for(int shot=0;shot<2;shot++){var a=refill.ForSeat(0);Check(refill.Fire(0,0,65,false,a.Version,5+shot,5+shot),"remaining rounds can still fire without ADS or number input");refill.Tick(5.6+shot);}
        var retained=refill.ForSeat(0);Check(retained.Ammo==4&&RouletteRules.Count(retained.SpentMask)==2&&retained.Action==DuelAction.CloseBarrel,"two fired cases and four live rounds remain distinct");
        int refillBefore=retained.CorrectInputs;for(int n=0;n<6;n++){var a=refill.ForSeat(0);Check(refill.Input(0,a.Digit(a.Progress),a.Version,7),"eject only spent cases");}
        Check(refill.ForSeat(0).LoadedMask==retained.LoadedMask&&refill.ForSeat(0).SpentMask==0&&refill.ForSeat(0).Action==DuelAction.Load,"ejection never removes unspent cartridges");
        Check(!refill.Input(0,1,retained.Version,7),"old pre-ejection command rejected");Load(refill,0,7);
        Check(refill.ForSeat(0).CorrectInputs-refillBefore==12&&refill.ForSeat(0).Ammo==6,"two-round top-up costs ejection plus exactly two loading sets");
        var saw=Director(DuelWeapon.SawedOff);Load(saw,0,4);
        for(int cycle=0;cycle<3;cycle++)
        {
            var a=saw.ForSeat(0);double at=5+cycle;
            Check(saw.Fire(0,0,65,false,a.Version,at,at),"sawed-off repeat cycle fires");saw.Tick(at+.6);
            Check(saw.ForSeat(0).Action==DuelAction.OpenBarrel&&saw.ForSeat(0).SpentMask==1,"sawed-off stays held and starts opening with spent shell");
            int before=saw.ForSeat(0).CorrectInputs;
            for(int n=0;n<4;n++){a=saw.ForSeat(0);Check(saw.Input(0,a.Digit(a.Progress),a.Version,at+.7),"opening reload action");}
            Check(saw.ForSeat(0).Action==DuelAction.Load&&saw.ForSeat(0).SpentMask==0&&saw.ForSeat(0).Ammo==0,"opening automatically ejects empty shell without granting ammo");
            Load(saw,0,at+.7);Check(saw.ForSeat(0).CorrectInputs-before==12,"later sawed-off load uses only open, insert and close");
        }
        var partial=Director();PrepareRevolver(partial,1,4);var load=partial.ForSeat(0);
        partial.Input(0,load.Digit(0),load.Version,4);load=partial.ForSeat(0);
        partial.Input(0,load.Digit(1)%5+1,load.Version,4);var wrong=partial.ForSeat(0);
        Check(wrong.Progress==0&&wrong.Ammo==1&&wrong.Mistakes==1,"wrong current cartridge never removes completed load");
        for(int i=0;i<3;i++)Check(wrong.Digit(i)!=load.Digit(i),"reroll current three-digit cartridge");
        var timed=Director();PrepareRevolver(timed,1,4);timed.Look(0,0,0,true,5);timed.Tick(8.99);
        Check(timed.ForSeat(0).Ammo==1&&timed.ForSeat(0).Action==DuelAction.Load,"partial ADS has full room shot window");
        Check(!timed.Fire(0,0,65,true,timed.ForSeat(0).Version,9,9),"partial ADS cannot bypass room deadline");timed.Tick(9);
        Check(timed.ForSeat(0).Ammo==0&&timed.ForSeat(0).Action==DuelAction.Pickup&&timed.Game.Shots==0,"expired partial aim reloads without fabricated shot");
    }
    static void Main()
    {
        ValidateTabletop();
        var preparingAI=new DuelDirector(DuelMode.AI,new PreciseRandom());for(int i=0;i<3;i++)preparingAI.Join(i,0);preparingAI.Ready(0,0);preparingAI.Tick(3);preparingAI.Tick(3+PartyGames.SelectionDuration+.01);
        Check(preparingAI.Clock.Phase==ClockPhase.ItemPreparation,"AI game has pre-start phase too");
        double itemsEnd=preparingAI.Clock.StartedAt;preparingAI.Tick(itemsEnd-.01);Check(preparingAI.ForSeat(1).Mistakes==0&&preparingAI.ForSeat(1).CorrectInputs==0,"AI waits during items window");preparingAI.Tick(itemsEnd+.01);double begins=preparingAI.Clock.StartedAt;preparingAI.Tick(begins-.01);
        Check(preparingAI.ForSeat(1).Mistakes==0&&preparingAI.ForSeat(1).CorrectInputs==0&&preparingAI.Game.Shots==0,"AI cannot act during countdown");
        preparingAI.Tick(begins);preparingAI.Tick(begins+.5);
        Check(preparingAI.QuickHandsActive&&preparingAI.ForSeat(1).Mistakes==1,"AI schedule starts only after countdown");
        foreach(DuelWeapon w in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff})foreach(DuelHitPart part in new[]{DuelHitPart.Head,DuelHitPart.Body,DuelHitPart.Legs})
        {
            var g=Game();int damage=WeaponRules.Damage(w,part);
            Check(g.HealthForSeat(2)==RouletteRules.MaxHealth,"max health100");
            Check(g.Shoot(0,2,w,part),"part shot accepted");Check(g.HealthForSeat(2)==100-damage,"fixed part damage");
            Check(g.LastDamageForSeat(2)==damage&&g.LastDamageForSeat(1)==(w==DuelWeapon.SawedOff?25:0),"per victim actual damage");
            Check(g.HealthForSeat(1)==(w==DuelWeapon.SawedOff?75:100),"splash25");
            while(RouletteRules.Has(g.AliveMask,2))Check(g.Shoot(0,2,w,part),"cumulative damage");
            Check(g.HealthForSeat(2)==0,"health never negative");Check(!g.Shoot(0,2,w,part),"dead target rejected");
        }
        var edge=Game();edge.Shoot(3,0,DuelWeapon.SawedOff,DuelHitPart.Body);
        Check(edge.HealthForSeat(1)==75&&edge.HealthForSeat(5)==100,"no wrap seat1 to6");
        edge.Shoot(3,5,DuelWeapon.SawedOff,DuelHitPart.Head);
        Check(edge.HealthForSeat(4)==75&&edge.HealthForSeat(0)==25,"no wrap reverse");
        edge.Shoot(1,0,DuelWeapon.SawedOff,DuelHitPart.Body);Check(edge.HealthForSeat(1)==75,"exclude shooter splash");
        var g2=Game();g2.Shoot(0,2,DuelWeapon.Revolver,DuelHitPart.Legs);g2.Shoot(0,2,DuelWeapon.Revolver,DuelHitPart.Legs);g2.Shoot(0,2,DuelWeapon.Revolver,DuelHitPart.Head);
        Check(g2.LastDamage==30,"overkill clamped actual damage");
        Check(g2.LastDamageForSeat(2)==30,"per victim fatal overkill clamped");
        Check(g2.Shoot(0,-1,DuelWeapon.SawedOff,DuelHitPart.None)&&g2.LastDamage==0&&g2.LastHitMask==0,"miss no splash");
        Check(g2.LastDamageForSeat(2)==0,"miss clears old damage record");
        foreach(DuelWeapon w in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff})
        {
            var d=Director(w);var before=d.ForSeat(0);
            Check(!d.Fire(0,0,0,false,before.Version,3,3),"cannot fire before load");
            Check(d.Look(0,5,2,true,3),"look while loading accepted");Check(d.ForSeat(0).Aiming==(w==DuelWeapon.Revolver)&&d.ForSeat(0).Version==before.Version,"empty revolver can ADS without changing inputs");
            Check(!d.Look(0,6,2,true,3.001),"look server throttle");
            Check(d.Input(0,before.Digit(0),before.Version,3),"look cannot block input");
            var prefix=d.ForSeat(0);d.Input(0,prefix.Digit(prefix.Progress)%5+1,prefix.Version,3);
            var after=d.ForSeat(0);Check(after.Progress==0&&after.Mistakes==1,"mistake reset");
            for(int i=0;i<after.Length;i++)Check(w==DuelWeapon.Revolver?after.Digit(i)!=prefix.Digit(i):after.Digit(i)==prefix.Digit(i),"reroll every digit only derringer");
            Check(!d.Input(0,after.Digit(0),after.Version,3.44),"mistake penalty");
            Load(d,0,4);Check(d.ForSeat(0).CorrectInputs==(w==DuelWeapon.Revolver?49:17),"five six-digit actions and six three-digit cartridges, or4x4");
            var a=d.ForSeat(0);Check(!d.Input(0,1,a.Version,4),"no numeric shooting");
            Check(!d.Fire(0,float.NaN,0,true,a.Version,4,4),"invalid angle rejected");
            Check(!d.Fire(0,0,0,true,a.Version,double.NaN,4),"invalid time rejected");
            Check(!d.Fire(0,0,0,true,a.Version,3.5,4),"old timestamp rejected");
            var other=d.ForSeat(5);
            Check(FireAt(d,0,3,DuelHitPart.Body,4),"authoritative body ray");
            Check(d.Game.HealthForSeat(3)==(w==DuelWeapon.SawedOff?25:50)&&d.ForSeat(0).LastHitPart==DuelHitPart.Body,"ray part damage");
            Check(!d.Fire(0,0,0,true,a.Version,4,4),"duplicate shot rejected");
            Check(d.ForSeat(5).Equals(other),"unrelated input and timers untouched by shot");
            Check(d.ForSeat(0).DamageDealt==(w==DuelWeapon.SawedOff?125:50),"shot stats actual splash sum");
            Check(d.ForSeat(0).FiredAliveMask==63&&d.ForSeat(0).FiredSpread==(w==DuelWeapon.SawedOff?3:0),"server stores actual visual rays before deaths");
            Check(d.ForSeat(0).Action==WeaponRules.ReloadAction(w)&&d.ForSeat(0).Ammo==(w==DuelWeapon.Revolver?5:0)&&d.Stage==DuelStage.Racing,"revolver retains remaining rounds, shotgun reloads without shared pause");
            Load(d,0,5);a=d.ForSeat(0);
            Check(d.Fire(0,0,65,false,a.Version,5,5),"miss launches and consumes loaded shot");d.Tick(5.6);
            Check(d.Game.LastTarget==-1&&d.Game.LastHitMask==0,"projectile miss has no splash");
            Load(d,0,6);Check(d.Look(0,0,0,true,6),"ADS");Check(d.ForSeat(0).Aiming,"aim active");d.Tick(6.36);Check(!d.ForSeat(0).Aiming&&d.ForSeat(0).Target==-1,"ADS timeout clears warning");
        }
        for(int actor=0;actor<6;actor++)for(int target=0;target<6;target++)if(actor!=target)
        {
            foreach(var part in new[]{DuelHitPart.Head,DuelHitPart.Body})
            {DuelGunplay.Angles(DuelGunplay.Point(target,part)-DuelGunplay.Eye(actor),out float y,out float p);var hit=DuelGunplay.Trace(actor,63,DuelGunplay.Direction(y,p));Check(hit.Seat==target&&hit.Part==part,"six seat head/body geometry "+actor+"/"+target+"/"+part);}
        }
        var blocked=DuelGunplay.Trace(0,63,DuelGunplay.Direction(0,-25));Check(blocked.Seat<0&&blocked.Distance<5,"table blocks low shot");
        var cone=DuelGunplay.TraceShotgun(0,63,DuelGunplay.Direction(5,-4),6);
        Check(DuelGunplay.Trace(0,63,DuelGunplay.Direction(5,-4)).Seat<0&&cone.Seat==3,"pellet edge can hit when narrow centre ray misses");
        Check(DuelGunplay.TraceShotgun(0,63,DuelGunplay.Direction(15,-4),6).Seat<0,"outside cone really misses");
        Check(DuelGunplay.TraceShotgun(0,63,DuelGunplay.Direction(0,-25),3).Seat<0,"all shotgun pellets blocked by table");
        int competing=0;
        for(int seed=0;seed<500;seed++)
        {
            var centre=DuelGunplay.Direction(-35,-4);int mask=0;
            for(int i=0;i<DuelGunplay.ShotgunPellets;i++){var h=DuelGunplay.Trace(0,63,DuelGunplay.PelletDirection(centre,40,i,seed));if(h.Seat>=0)mask|=1<<h.Seat;}
            if(!RouletteRules.Has(mask,1)||!RouletteRules.Has(mask,2))continue;
            competing++;Check(DuelGunplay.TraceShotgun(0,63,centre,40,seed).Seat==2,"centre priority over a nearer world-space player");
        }
        Check(competing>0,"multiple actual pellet contacts exercise centre priority");
        Check(DuelGunplay.TraceShotgun(0,1,DuelGunplay.Direction(0,0),6).Seat<0,"exclude self and dead targets");
        for(int actor=0;actor<6;actor++)for(int target=0;target<6;target++)if(actor!=target)
        {
            DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Head)-DuelGunplay.Eye(actor),out float y,out float p);
            var h=DuelGunplay.TraceShotgun(actor,63,DuelGunplay.Direction(y,p),3);
            Check(h.Seat<0||h.Seat==target&&h.Part>=DuelHitPart.Head&&WeaponRules.Damage(DuelWeapon.SawedOff,h.Part)==75,"shotgun preserves contact region while every region deals 75");
        }
        foreach(float pitch in new[]{-65f,0f,65f})foreach(float radius in new[]{3f,6f})
        {
            var centre=DuelGunplay.Direction(73,pitch);
            for(int i=0;i<DuelGunplay.ShotgunPellets;i++)
            {
                var pellet=DuelGunplay.PelletDirection(centre,radius,i);
                Check(Math.Abs(AimVector.Dot(pellet,pellet)-1)<.00001,"unit pellet ray");
                Check(AimVector.Dot(centre,pellet)>=(float)Math.Cos(radius*Math.PI/180)-.000001,"pellet cannot escape finite cone");
            }
        }
        DuelGunplay.Sway(0,DuelWeapon.Revolver,true,4,0,out float dy,out float dp);
        DuelGunplay.Sway(0,DuelWeapon.SawedOff,true,4,0,out float sy2,out float sp2);
        Check(Math.Abs(dy/sy2-2)<.0001&&Math.Abs(dp/sp2-2)<.0001,"derringer sway twice sawed-off on both axes");
        Check(Math.Abs(sy2-Math.Sin(4*7.1)*3.15)<.0001&&Math.Abs(sp2-Math.Sin(4*5.3)*2.205)<.0001,"ADS keeps full sawed-off sway");
        DuelGunplay.Sway(0,DuelWeapon.SawedOff,false,4,0,out float hipYaw,out float hipPitch);
        Check(Math.Abs(hipYaw-Math.Sin(4*7.1)*3.15)<.0001&&Math.Abs(hipPitch-Math.Sin(4*5.3)*2.205)<.0001,"sawed-off inherits former derringer hip sway");
        for(int s=0;s<6;s++)foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.SawedOff})
        {
            DuelGunplay.Sway(s,weapon,false,7,0,out float hy,out float hp);
            DuelGunplay.Sway(s,weapon,true,7,7,out float ay,out float ap);
            DuelGunplay.Sway(s,weapon,true,7,0,out float settledYaw,out float settledPitch);
            Check(hy==ay&&hp==ap&&ay==settledYaw&&ap==settledPitch,"ADS toggle and hold duration never reduce sway");
        }
        Check(WeaponRules.Damage(DuelWeapon.SawedOff,DuelHitPart.Head)==75&&WeaponRules.Damage(DuelWeapon.SawedOff,DuelHitPart.Legs)==75,"all shotgun parts75");
        var patternA=DuelGunplay.PelletDirection(DuelGunplay.Direction(0,0),6,0,77);
        var patternB=DuelGunplay.PelletDirection(DuelGunplay.Direction(0,0),6,0,78);
        Check(AimVector.Dot(patternA,patternB)<.999999,"new seed changes shotgun pattern");
        var flying=Director(DuelWeapon.SawedOff);Load(flying,0,4);
        DuelGunplay.Angles(DuelGunplay.Point(3,DuelHitPart.Body)-DuelGunplay.Eye(0),out float fy,out float fp);
        DuelGunplay.Sway(0,DuelWeapon.SawedOff,true,4,4,out float fsy,out float fsp);
        Check(flying.Fire(0,fy-fsy,fp-fsp,true,flying.ForSeat(0).Version,4,4),"projectile launched");
        Check(flying.Game.HealthForSeat(3)==100&&flying.ForSeat(0).Action==DuelAction.OpenBarrel,"launch does not instantly damage, starts held reload");
        var peer=flying.ForSeat(5);Check(flying.Input(5,peer.Digit(0),peer.Version,4.01),"other player's input during projectile flight");
        flying.Tick(4.01);Check(flying.Game.HealthForSeat(3)==100,"finite-speed bullet cannot arrive early");
        flying.Tick(4.3);Check(flying.Game.HealthForSeat(3)==25&&flying.Game.HealthForSeat(2)==75&&flying.Game.HealthForSeat(4)==75,"one volley direct75 then adjacent25, not per-pellet damage");
        var timed=Director();Load(timed,0,4);int timedVersion=timed.ForSeat(0).Version;
        Check(!timed.Fire(0,0,0,true,timedVersion,8,8),"quick shot denied at limit");timed.Tick(8);
        Check(timed.ForSeat(0).Action==DuelAction.Pickup&&timed.ForSeat(0).Version!=timedVersion&&timed.Game.Shots==0,"time expiry reloads without fake shot or kill");
        var room=Game();Check(!room.SetShotTime(0),"settings cannot change during play");
        Check(!room.StartWithItems&&!room.SetStartWithItems(true),"start items default off and locked during play");
        room=new RouletteRules(DuelMode.PvP);room.Join(0);room.Join(1);room.SetReady(1);
        Check(room.SetShotTime(0)&&room.ReadyMask==2,"shot time setting preserves readiness");
        room.SetReady(0);Check(room.SetStartWithItems(true)&&room.StartWithItems&&room.ReadyMask==3,"start items setting preserves readiness");
        Check(!room.UnlimitedItems&&room.SetUnlimitedItems(true)&&room.UnlimitedItems&&room.ReadyMask==3,"unlimited items defaults off and preserves readiness");
        Check(!room.SetUnlimitedItems(true)&&room.ReadyMask==3&&room.SetUnlimitedItems(false)&&room.ReadyMask==3,"unlimited items off and duplicate preserve readiness");
        Check(room.SetManualMiniGame(true)&&room.ReadyMask==3&&room.SetManualMiniGame(false)&&room.ReadyMask==3,"manual selection settings preserve readiness");
        Check(!room.SetStartWithItems(true)&&room.SetStartWithItems(false)&&!room.StartWithItems,"item setting toggles without duplicate changes");
        Check(!room.SetShotTime(float.NaN)&&!room.SetShotTime(float.PositiveInfinity)&&!room.SetShotTime(-1)&&!room.SetShotTime(61),"invalid room limits rejected");
        var unlimited=Director(DuelWeapon.Revolver,0);Load(unlimited,0,4);unlimited.Tick(100);
        Check(unlimited.ForSeat(0).Action==DuelAction.Aim&&FireAt(unlimited,0,3,DuelHitPart.Body,100),"unlimited quick shot remains available");
        var traded=Director();Load(traded,0,4);Load(traded,3,4);
        foreach(int actor in new[]{0,3})
        {
            int victim=3-actor;DuelGunplay.Angles(DuelGunplay.Point(victim,DuelHitPart.Head)-DuelGunplay.Eye(actor),out float y,out float p);
            DuelGunplay.Sway(actor,DuelWeapon.Revolver,true,4,4,out float sy,out float sp);
            Check(traded.Fire(actor,y-sy,p-sp,true,traded.ForSeat(actor).Version,4,4),"simultaneous projectile launch");
        }
        traded.Tick(4.3);Check(!RouletteRules.Has(traded.Game.AliveMask,0)&&!RouletteRules.Has(traded.Game.AliveMask,3)&&traded.ForSeat(0).Kills==1&&traded.ForSeat(3).Kills==1,"same-tick projectiles trade; dead shooter cannot erase an airborne bullet");
        var lateTrade=new DuelDirector(DuelMode.PvP,new PreciseRandom());lateTrade.Join(0,-16);lateTrade.Join(3,-16);lateTrade.Ready(0,-16);lateTrade.Ready(3,-16);lateTrade.Tick(-13.5);lateTrade.Tick(-7.69);lateTrade.Tick(-2.68);lateTrade.Tick(.33);
        lateTrade.Game.Shoot(0,3,DuelWeapon.Revolver,DuelHitPart.Body);lateTrade.Game.Shoot(3,0,DuelWeapon.Revolver,DuelHitPart.Body);
        Load(lateTrade,0,4);Load(lateTrade,3,4);
        foreach(int actor in new[]{0,3})
        {
            double at=actor==0?4:4.05;DuelGunplay.Angles(DuelGunplay.Point(3-actor,DuelHitPart.Body)-DuelGunplay.Eye(actor),out float y,out float p);
            DuelGunplay.Sway(actor,DuelWeapon.Revolver,true,at,at,out float sy,out float sp);
            Check(lateTrade.Fire(actor,y-sy,p-sp,true,lateTrade.ForSeat(actor).Version,at,at),"staggered projectile launch");
        }
        lateTrade.Tick(4.2);Check(lateTrade.Game.Phase==DuelPhase.Playing&&RouletteRules.Count(lateTrade.Game.AliveMask)==1&&lateTrade.ForSeat(3).Kills==0,"one survivor waits for already airborne bullet");
        lateTrade.Tick(4.35);Check(lateTrade.ForSeat(0).Kills==1&&lateTrade.ForSeat(3).Kills==1,"later arrival from fallen shooter still resolves before victory");
        int legHits=0;
        for(int actor=0;actor<6;actor++)for(int target=0;target<6;target++)if(actor!=target)
            foreach(float x in new[]{-.3f,.3f})foreach(float y in new[]{.2f,.35f,.50f})
            {
                var point=DuelGunplay.Local(target,x,y,.1f);
                DuelGunplay.Angles(point-DuelGunplay.Eye(actor),out float yaw,out float pitch);
                var hit=DuelGunplay.Trace(actor,63,DuelGunplay.Direction(yaw,pitch));
                if(hit.Seat==target&&hit.Part==DuelHitPart.Legs)legHits++;
            }
        Check(legHits==0,"the tabletop covers lowered seated legs from other seats");
        for(int target=0;target<6;target++)
        {
            var origin=DuelGunplay.Local(target,1,.3f,0);
            var exposed=DuelGunplay.Local(target,.22f,.3f,0)-origin;
            DuelGunplay.Angles(exposed,out float sideYaw,out float sidePitch);
            var hit=DuelGunplay.TraceFrom((target+3)%6,63,origin,DuelGunplay.Direction(sideYaw,sidePitch));
            Check(hit.Seat==target&&hit.Part==DuelHitPart.Legs,"uncovered lower leg geometry still receives leg damage "+target);
        }
        Check(!DuelGunplay.ValidAngles(0,66)&&!DuelGunplay.ValidAngles(float.PositiveInfinity,0),"bounded aim");
        Check(Math.Abs(DuelGunplay.AdsFov(DuelWeapon.Revolver)-57.46)<.1&&Math.Abs(DuelGunplay.AdsFov(DuelWeapon.SawedOff)-65.18)<.2,"weapon optical zoom");
        var win=Director();for(int target=1;target<6;target++){Load(win,0,4+target);Check(FireAt(win,0,target,DuelHitPart.Head,4+target),"headshot kill");}
        Check(win.Clock.First==0&&win.Clock.Last==1&&win.ForSeat(0).Kills==5&&win.ForSeat(0).DamageDealt==500,"mini winner and statistics");
        Check(!win.Ready(0,10)&&win.PartyAliveMask==63&&win.ForSeat(1).LivesRemaining==2,"mini result consumes one life without permanently removing victims");
        double now=win.Clock.PhaseAt+DuelClockContest.QuickResultsDuration+.01;win.Tick(now);
        while(RouletteRules.Count(win.PartyAliveMask)>1)
        {
            now=win.Clock.PhaseAt+PartyGames.SelectionDuration+.01;win.Tick(now);
            if(win.Clock.MiniGame==PartyMiniGame.Countdown)
            {
                var c=win.Clock;double goal=c.StartedAt+c.Target/100.0;
                for(int i=0;i<6;i++)if(RouletteRules.Has(win.Game.AliveMask,i))win.StopClock(i,c.Iteration,goal+i*.1,goal+i*.1);
                win.Tick(win.Clock.PhaseAt+DuelClockContest.RevealDuration+.01);win.Tick(win.Clock.StartedAt+.01);now=win.Clock.PhaseAt+.1;
                Check(FireAt(win,0,win.Clock.Last,DuelHitPart.Head,now),"alternating clock shot consumes a life");
            }
            else
            {
                win.Tick(win.Clock.StartedAt+.01);win.Tick(win.Clock.StartedAt+.01);now=win.Clock.StartedAt+.1;
                for(int target=1;target<6;target++)if(RouletteRules.Has(win.Game.AliveMask,target)){now+=1;Load(win,0,now);Check(FireAt(win,0,target,DuelHitPart.Head,now),"later quick headshot");}
            }
            if(win.Game.Phase==DuelPhase.Playing){now=win.Clock.PhaseAt+(win.Clock.Phase==ClockPhase.QuickResults?DuelClockContest.QuickResultsDuration:DuelClockContest.ReturnDuration)+.01;win.Tick(now);}
        }
        win.Tick(now+2);for(int i=0;i<5;i++)win.Ready(i,now+2);Check(win.Stage==DuelStage.Idle,"dead last player must ready");win.Ready(5,now+2);Check(win.Stage==DuelStage.Countdown&&win.ForSeat(0).Kills==0&&win.Game.HealthForSeat(5)==100&&win.ForSeat(0).LastHitPart==DuelHitPart.None,"rematch resets stats health");
        foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})for(int bots=1;bots<=5;bots++)
        {
            var ai=new DuelDirector(DuelMode.AI,new Random(177+bots),difficulty);for(int i=0;i<=bots;i++)ai.Join(i,0);ai.Ready(0,0);
            for(double t=0;t<3600&&ai.Game.Phase!=DuelPhase.Finished;t+=.05)ai.Tick(t);
            Check(ai.Game.Phase==DuelPhase.Finished&&RouletteRules.Count(ai.Game.AliveMask)==1&&ai.PartyAliveMask==ai.Game.AliveMask,"AI finishes with one persistent survivor "+difficulty+"/"+bots+" phase "+ai.Game.Phase+" alive "+ai.Game.AliveMask+" party "+ai.PartyAliveMask+" clock "+ai.Clock.Phase+" shots "+ai.Game.Shots);
        }
        foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})
        {
            var ai=new DuelDirector(DuelMode.AI,new QuickRandom(918),difficulty);for(int i=0;i<6;i++)ai.Join(i,0);ai.Game.SetShotTime(1);ai.Ready(0,0);
            for(double t=0;t<900&&ai.Game.Shots<3;t+=.05)ai.Tick(t);
            Check(ai.Game.Shots>=3,"AI can fire in quick hands with shortest shot limit "+difficulty);
        }
        ValidateRevolver();Console.WriteLine("PASS gunplay: "+checks+" checks");
    }
    static void ValidateTabletop()
    {
        var room=new RouletteRules(DuelMode.PvP);room.Join(0);room.Join(1);room.SetReady(1);room.SetShotTime(0);room.SetStartWithItems(true);room.SetUnlimitedItems(true);room.SetManualMiniGame(true);
        Check(room.ResetDeveloperSettings()&&room.ReadyMask==2&&room.ShotTimeLimit==10&&!room.StartWithItems&&!room.UnlimitedItems&&!room.ManualMiniGame,"closing developer mode restores all defaults and preserves readiness");
        Check(!room.ResetDeveloperSettings(),"default reset is idempotent");
        var developer=new DuelDirector(DuelMode.PvP,new PreciseRandom());developer.Join(0,0);developer.Join(1,0);developer.Game.SetManualMiniGame(true);developer.Ready(0,0);developer.Ready(1,0);developer.Tick(3);
        int oldIteration=developer.Clock.Iteration;developer.ResetDeveloperSettings(3.1);
        Check(!developer.Game.ManualMiniGame&&developer.Clock.Iteration==oldIteration+1&&developer.Clock.MiniGame==PartyMiniGame.QuickHands,"disabling manual selection restarts a real random reel rather than keeping its placeholder");
        developer.Tick(9);Check(developer.Clock.Phase==ClockPhase.ItemPreparation,"automatic reel proceeds without another host choice");
        var t=new DuelTabletop();t.Reset(0);t.Tick(1,true,2);Check(t.State.Seat0.Action==DuelDrinkAction.Pour,"idle starts pouring");
        t.Tick(1,true,4.9);Check(RouletteRules.Has(t.State.FilledMask,0),"pour fills glass before return motion ends");
        t.Tick(1,true,6.7);t.Tick(1,true,7.1);Check(t.State.Seat0.Action==DuelDrinkAction.Sip,"filled glass is drunk next");
        t.Tick(1,true,9.5);Check(RouletteRules.Has(t.State.DrankMask,0)&&!RouletteRules.Has(t.State.FilledMask,0),"one sip consumes drink and marks completed");
        t.Tick(1,true,11);t.Tick(1,true,100);Check(t.State.Seat0.Action==DuelDrinkAction.None,"drinking never repeats within party");
        t.Break(0,DuelTableProp.Glass,101);t.Tick(1,true,103);Check(t.State.Seat0.Action==DuelDrinkAction.None,"finished drink prevents later bottle inspection too");
        t.Reset(0);t.Tick(1,true,2);t.Break(0,DuelTableProp.Bottle,2.2);t.Tick(1,true,4);Check(t.State.Seat0.Action==DuelDrinkAction.None&&!RouletteRules.Has(t.State.FilledMask,0),"broken bottle cancels pouring and cannot fill glass");
        t.Reset(0);t.Tick(1,true,2);t.Tick(1,true,4.9);t.Break(0,DuelTableProp.Bottle,5);t.Tick(1,true,6.1);Check(t.State.Seat0.Action==DuelDrinkAction.Sip,"already-filled glass can be drunk after bottle breaks");
        t.Reset(0);t.Break(0,DuelTableProp.Glass,.5);t.Tick(1,true,2);Check(t.State.Seat0.Action==DuelDrinkAction.Inspect,"broken glass permits only bottle inspection");
        t.Break(0,DuelTableProp.Bottle,2.1);t.Tick(1,true,10);Check(t.State.Seat0.Action==DuelDrinkAction.None,"both objects broken disables drinking activities");
        t.Reset(0);t.Tick(1,true,2);t.Tick(1,true,4.9);t.Tick(1,false,5);Check(t.State.Seat0.Action==DuelDrinkAction.None&&RouletteRules.Has(t.State.FilledMask,0),"gun and item phases cancel hand activity but preserve filled glass");
        t.Tick(1,true,6.1);Check(t.State.Seat0.Action==DuelDrinkAction.Sip,"activity resumes without a second pour");
        t.Tick(0,true,6.2);Check(t.State.Seat0.Action==DuelDrinkAction.None,"death or disconnect releases held prop");
        t.Reset(7);Check(t.State.Equals(default(DuelTabletopState)),"new party restores bottles, glasses and drinking permission");
        Check(!t.Break(-1,DuelTableProp.Bottle,8)&&!t.Break(6,DuelTableProp.Glass,8)&&!t.Break(0,(DuelTableProp)255,8),"invalid prop requests rejected");
        for(int seat=0;seat<6;seat++)foreach(DuelTableProp kind in new[]{DuelTableProp.Bottle,DuelTableProp.Glass})
        {
            t.Reset(0);var target=DuelTabletop.Rest(seat,kind);var forward=DuelGunplay.Forward(seat);var origin=target+forward*.7f;
            var ray=forward*-1;var hit=DuelGunplay.TraceFrom((seat+3)%6,0,origin,ray,t.State);
            Check(hit.Prop==kind&&hit.PropSeat==seat&&hit.Seat==-1,"each faceted prop has its own server hit region");
            var volley=new DuelProjectileVolley((seat+3)%6,1,1,DuelWeapon.Revolver,origin,ray,0,0,0);
            Check(!volley.Tick(0,.001,out hit,t)&&!t.State.Broken(seat,kind),"finite flight cannot break before reaching glass");
            volley.Tick(0,.03,out hit,t);Check(t.State.Broken(seat,kind)&&hit.Seat==-1,"actual projectile breaks prop without player damage");
            var snapshot=t.State;Check(!t.Break(seat,kind,.04)&&t.State.Equals(snapshot),"same prop breaks only once");
            var rest=DuelTabletop.Rest(seat,kind);Check(rest.Y>=.99f,"prop stays above tabletop");
        }
    }
}
