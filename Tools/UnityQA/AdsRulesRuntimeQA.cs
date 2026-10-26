using System;
using System.Collections;
using System.IO;
using System.Reflection;
using HelloWorld;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed class AdsRulesRuntimeQA:MonoBehaviour
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Port="18993";
    string role,path;int checks;double deadline;HelloWorldManager manager;HelloWorldPlayer player;
    T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
    DuelDirector D=>Field<DuelDirector>(manager,"director");double Now=>player.NetworkManager.ServerTime.Time;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--ads-qa");if(i<0)return;
        var q=new GameObject("ADS rules QA").AddComponent<AdsRulesRuntimeQA>();q.role=a[i+1];q.path=a[Array.IndexOf(a,"--ads-dir")+1];Directory.CreateDirectory(q.path);q.deadline=Time.realtimeSinceStartupAsDouble+120;Application.targetFrameRate=30;
    }
    void Finish(string s,int code=0)
    {
        Debug.Log(s);File.WriteAllText(Path.Combine(path,role+"-done"),s);deadline=double.MaxValue;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(code);
#else
        Application.Quit(code);
#endif
    }
    void Check(bool ok,string s){checks++;if(!ok){Finish("FAIL ADS "+role+": "+s,1);throw new Exception(s);}Debug.Log("ADS check: "+s);}
    void Update(){if(Time.realtimeSinceStartupAsDouble>deadline)Finish("FAIL ADS timeout "+role,1);}
    void Mark(string s)=>File.WriteAllText(Path.Combine(path,role+"-"+s),"ok");bool Seen(string r,string s)=>File.Exists(Path.Combine(path,r+"-"+s));
    void Publish()=>typeof(HelloWorldManager).GetMethod("Publish",F).Invoke(manager,null);
    void Racing(DuelDirector d,double now,int mask)
    {
        typeof(DuelDirector).GetMethod("SetStage",F).Invoke(d,new object[]{DuelStage.Racing,now});
        typeof(DuelClockContest).GetProperty("State").SetValue(Field<DuelClockContest>(d,"clock"),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,ParticipantsMask=mask,StartedAt=now});
    }
    void Loaded(DuelDirector d,int seat,DuelWeapon weapon,double now)
    {var a=Field<DuelActionState[]>(d,"actions");a[seat].Weapon=weapon;a[seat].Action=DuelAction.Aim;a[seat].LoadedMask=1;a[seat].SpentMask=0;a[seat].StartedAt=now;a[seat].ShootingStartedAt=now;a[seat].Version++;}
    DuelDirector Setup(DuelMode mode=DuelMode.PvP)
    {
        var d=new DuelDirector(mode,new System.Random(31),AiDifficulty.Hard);d.Join(0,0);d.Join(1,0);d.SetAutoSelectItems(true);d.SelectWeapon(0,DuelWeapon.SawedOff,0);d.SelectWeapon(1,DuelWeapon.SawedOff,0);d.Ready(0,0);d.Ready(1,0);Racing(d,10,3);Loaded(d,0,DuelWeapon.SawedOff,10);Loaded(d,1,DuelWeapon.SawedOff,10);return d;
    }
    sealed class FixedRadiusRandom:System.Random{public override double NextDouble()=>.5;}
    DuelDirector PrecisionSetup(DuelWeapon weapon,bool practice)
    {
        var d=new DuelDirector(DuelMode.PvP,new FixedRadiusRandom());for(int i=0;i<3;i++)d.Join(i,0);
        d.SetAutoSelectItems(true);d.SelectWeapon(0,weapon,0);d.Ready(0,0);
        if(!practice){d.Ready(1,0);d.Ready(2,0);Racing(d,10,7);Loaded(d,0,weapon,10);}return d;
    }
    static float OffsetRadius(DuelActionState a,float yaw)=>Mathf.Sqrt((a.FiredYaw-yaw)*(a.FiredYaw-yaw)+a.FiredPitch*a.FiredPitch);
    void PrecisionRules()
    {
        Check(WeaponRules.Name(DuelWeapon.BAS)=="저격소총","sniper display name");
        for(int i=0;i<=20;i++)
        {
            float revolver=DuelGunplay.PrecisionSpreadRadius(DuelWeapon.Revolver,false,i/20.0),sniper=DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,false,i/20.0);
            Check(revolver==sniper&&revolver>=35&&revolver<=70,"identical centre-excluding hip radius sample "+i);
            Check(DuelGunplay.PrecisionSpreadRadius(DuelWeapon.Revolver,true,i/20.0)==0&&DuelGunplay.PrecisionSpreadRadius(DuelWeapon.BAS,true,i/20.0)==0,"ADS adds no random spread "+i);
        }
        foreach(var weapon in new[]{DuelWeapon.Revolver,DuelWeapon.BAS})
        {
            foreach(bool practice in new[]{false,true})
            {
                var d=PrecisionSetup(weapon,practice);Check(d.Fire(0,0,0,false,d.ForSeat(0).Version,10.2,10.2),"actual hip fire accepted "+weapon+" practice="+practice);
                var a=d.ForSeat(0);Check(Mathf.Abs(OffsetRadius(a,0)-70*Mathf.Sqrt(.625f))<.001f&&!a.FiredAds&&a.FiredSpread==0,"actual server hip annulus without sway "+weapon+" practice="+practice);
            }
            var ads=PrecisionSetup(weapon,false);Check(ads.Fire(0,0,0,true,ads.ForSeat(0).Version,10.2,10.2),"actual ADS fire accepted "+weapon);
            DuelGunplay.Sway(0,weapon,true,10.2,10.2,out float y,out float p);Check(Mathf.Abs(ads.ForSeat(0).FiredYaw-y)<.001f&&Mathf.Abs(ads.ForSeat(0).FiredPitch-p)<.001f,"ADS follows weapon sway exactly "+weapon);
        }
    }
    void Model()
    {
        for(int seat=0;seat<6;seat++)foreach(DuelWeapon w in Enum.GetValues(typeof(DuelWeapon)))
        {
            DuelGunplay.Sway(seat,w,false,13.7,0,out var y,out var p);Check(y==0&&p==0,"hip sway disabled: "+seat+"/"+w);
            DuelGunplay.Sway(seat,w,true,13.7,0,out y,out p);Check(w==DuelWeapon.SawedOff?y==0&&p==0:Math.Abs(y)+Math.Abs(p)>.01,"ADS sway respects capability: "+seat+"/"+w);
        }
        Check(!WeaponRules.SupportsAds(DuelWeapon.SawedOff)&&WeaponRules.SupportsAds(DuelWeapon.Revolver)&&WeaponRules.SupportsAds(DuelWeapon.BAS),"ADS capability");
        Check(DuelGunplay.Spread(DuelWeapon.SawedOff,false)==6&&DuelGunplay.Spread(DuelWeapon.SawedOff,true)==6,"shotgun fixed spread regardless of submitted flag");
        Check(DuelGunplay.ShotRange(DuelWeapon.SawedOff,false)==20&&DuelGunplay.ShotRange(DuelWeapon.SawedOff,true)==20,"shotgun fixed range");
        Check(DuelGunplay.AdsFov(DuelWeapon.SawedOff)==75,"shotgun has no zoom");
        Check(DuelGunplay.Spread(DuelWeapon.Revolver,false)==70&&DuelGunplay.Spread(DuelWeapon.BAS,false)==70&&DuelGunplay.Spread(DuelWeapon.Revolver,true)==0&&DuelGunplay.Spread(DuelWeapon.BAS,true)==0,"precision hip penalty and ADS accuracy preserved");
        var centre=DuelGunplay.Direction(0,0);Check(DuelGunplay.MuzzleOrigin(0,DuelWeapon.SawedOff,true,centre).Equals(DuelGunplay.MuzzleOrigin(0,DuelWeapon.SawedOff,false,centre)),"shotgun cannot move ballistic origin via ADS flag");
        Check(!DuelGunplay.PelletDirection(centre,6,0,1).Equals(DuelGunplay.PelletDirection(centre,6,0,2)),"new shotgun seeds change pellet pattern");
        DuelGunplay.Angles(DuelGunplay.Point(3,DuelHitPart.Body)-DuelGunplay.Eye(0),out float yaw,out float pitch);centre=DuelGunplay.Direction(yaw,pitch);
        bool reached=false;for(int seed=1;seed<=12;seed++)
        {var v=new DuelProjectileVolley(0,1,1,DuelWeapon.SawedOff,DuelGunplay.MuzzleOrigin(0,DuelWeapon.SawedOff,false,centre),centre,6,seed,0);if(v.Tick(9,1,out var hit)&&hit.Seat==3)reached=true;}
        Check(reached,"actual shotgun pellets can reach opposite seat beyond old 6m range");
        var away=DuelGunplay.Direction(28,0);var miss=new DuelProjectileVolley(0,1,1,DuelWeapon.SawedOff,DuelGunplay.MuzzleOrigin(0,DuelWeapon.SawedOff,false,away),away,6,11,0);miss.Tick(9,1,out var missed);Check(missed.Seat<0,"off-target shotgun remains a miss");
        var yes=Setup();var no=Setup();Check(yes.Look(0,0,0,true,10.1)&&!yes.ForSeat(0).Aiming,"server ignores unsupported ADS look");
        Check(yes.Fire(0,0,0,true,yes.ForSeat(0).Version,10.2,10.2)&&no.Fire(0,0,0,false,no.ForSeat(0).Version,10.2,10.2),"normal match accepts either shotgun flag");
        var a=yes.ForSeat(0);var b=no.ForSeat(0);Check(!a.FiredAds&&a.FiredSpread==6&&a.FiredYaw==0&&a.FiredPitch==0&&a.FiredSeed==b.FiredSeed&&a.FiredOrigin.Equals(b.FiredOrigin),"server shot has identical hip ballistics and no hidden sway");
        Check(a.Action==DuelAction.OpenBarrel&&a.Ammo==0&&a.SpentMask==1,"shotgun reload cycle unchanged");
        var ai=Setup(DuelMode.AI);for(int i=0;i<600&&ai.ForSeat(1).ShotsFired<2;i++)ai.Tick(10.1+i*.08);
        Check(ai.ForSeat(1).ShotsFired>=2,"shotgun AI aims and fires again without ADS state");
        PrecisionRules();DuelHighlightsChecks.Validate();Check(true,"existing factual highlights threshold and serialization regression");
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(.7f);manager=HelloWorldManager.Instance;if(role=="model")Model();else yield return Peers();Finish("PASS ADS "+role+": "+checks+" checks");
    }
    IEnumerator Peers()
    {
        if(role=="client")while(!Seen("host","listening"))yield return null;
        manager.Begin(DuelMode.PvP,role=="client","127.0.0.1",Port);while(HelloWorldPlayer.Local==null)yield return null;player=HelloWorldPlayer.Local;
        Check(player.NetworkManager.NetworkConfig.ProtocolVersion==DuelLanRooms.Protocol,"current protocol");
        if(role=="host")
        {
            Mark("listening");while(D.Game.ConnectedMask!=3)yield return null;D.Join(2,Now);D.SetAutoSelectItems(true);D.Ready(0,Now);Publish();Mark("setup");
            while(!Seen("client","practice"))yield return null;
            Check(!D.ForSeat(1).FiredAds&&D.ForSeat(1).FiredSpread==6,"remote practice flag normalized on server");Mark("saw-verified");
            while(!Seen("client","precision-practice"))yield return null;
            Check(D.ForSeat(1).FiredWeapon==DuelWeapon.Revolver&&OffsetRadius(D.ForSeat(1),60)>=35&&OffsetRadius(D.ForSeat(1),60)<=70,"remote practice revolver has sniper hip spread");
            D.Ready(2,Now);Racing(D,Now,7);Loaded(D,1,DuelWeapon.Revolver,Now);Publish();Mark("match");
            while(!Seen("client","match"))yield return null;Check(!D.ForSeat(1).FiredAds&&D.ForSeat(1).ShotsFired==1&&D.ForSeat(1).Ammo==0,"remote match shot authorized and consumes ammo");Mark("verified");
            while(!Seen("client","finished"))yield return null;
        }
        else
        {
            while(!Seen("host","setup")||player.State.Value.Seat1.SelectedItems!=DuelLoadout.All)yield return null;
            player.SelectWeapon(DuelWeapon.SawedOff);while(player.State.Value.Seat1.SelectedWeapon!=DuelWeapon.SawedOff)yield return null;player.Rematch();while(!player.State.Value.IsPracticing(1))yield return null;
            var keyboard=InputSystem.AddDevice<Keyboard>();InputState.Change(keyboard,new KeyboardState());keyboard.MakeCurrent();
            var mouse=InputSystem.AddDevice<Mouse>();InputState.Change(mouse,new MouseState{buttons=2});mouse.MakeCurrent();yield return new WaitForSecondsRealtime(.2f);
            var fps=manager.GetComponent<DuelFirstPerson>();Check(!fps.Ads,"right mouse cannot activate local shotgun ADS");
            Check(Camera.main.fieldOfView==DuelGunplay.BaseFov,"shotgun right mouse keeps normal camera FOV");
            Check(Quaternion.Angle(fps.Rotation(Now),fps.Rotation(Now+.37))<.001,"shotgun practice camera remains steady");
            player.Look(60,0,true);yield return new WaitForSecondsRealtime(.2f);Check(!player.State.Value.Seat1.Aiming,"owner sees server rejecting shotgun ADS");
            player.Fire(60,0,true,Now);while(player.State.Value.Seat1.PracticeShotsFired==0)yield return null;
            Check(!player.State.Value.Seat1.FiredAds&&player.State.Value.Seat1.FiredSpread==6,"owner receives normalized practice shot");Mark("practice");
            while(!Seen("host","saw-verified"))yield return null;
            InputState.Change(mouse,new MouseState());mouse.MakeCurrent();player.Rematch();while(player.State.Value.IsPracticing(1))yield return null;
            player.SelectWeapon(DuelWeapon.Revolver);while(player.State.Value.Seat1.SelectedWeapon!=DuelWeapon.Revolver)yield return null;
            player.Rematch();while(!player.State.Value.IsPracticing(1))yield return null;
            int previous=player.State.Value.Seat1.PracticeShotsFired;player.Fire(60,0,false,Now);while(player.State.Value.Seat1.PracticeShotsFired<=previous)yield return null;
            Check(!player.State.Value.Seat1.FiredAds&&OffsetRadius(player.State.Value.Seat1,60)>=35&&OffsetRadius(player.State.Value.Seat1,60)<=70,"remote owner practice revolver excludes centre");Mark("precision-practice");
            while(!Seen("host","match")||player.State.Value.Clock.Phase!=ClockPhase.QuickHands||player.State.Value.Seat1.Ammo!=1)yield return null;
            player.Fire(60,0,false,Now);while(player.State.Value.Seat1.ShotsFired==0)yield return null;
            Check(!player.State.Value.Seat1.FiredAds&&OffsetRadius(player.State.Value.Seat1,60)>=35&&OffsetRadius(player.State.Value.Seat1,60)<=70,"normal remote revolver shot uses sniper hip spread");Mark("match");
            while(!Seen("host","verified"))yield return null;Mark("finished");
        }
    }
}
