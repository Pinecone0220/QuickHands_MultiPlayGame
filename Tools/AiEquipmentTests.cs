using System;
using System.Collections.Generic;
using System.Reflection;
using HelloWorld;
class AiEquipmentTests
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    sealed class Middle:Random
    {public override double NextDouble()=>.5;public override int Next(int max)=>0;public override int Next(int min,int max)=>min;}
    static DuelClockContest Clock(DuelDirector d)=>(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);
    static void Stage(DuelDirector d,double now)=>typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,now});
    static DuelDirector Party(int bots=5,AiDifficulty difficulty=AiDifficulty.Normal,bool all=false,bool unlimited=false)
    {
        var d=new DuelDirector(DuelMode.AI,new Middle(),difficulty);d.Join(0,0);d.SelectWeapon(0,DuelWeapon.BAS,0);d.SelectItem(0,DuelItem.Recovery);d.SelectItem(0,DuelItem.Taser);
        for(int i=1;i<=bots;i++)d.Join(i,0);d.Game.SetShotTime(0);d.Game.SetUnlimitedItems(unlimited);if(all)d.SetAutoSelectItems(true);d.Ready(0,0);return d;
    }
    static void Prep(DuelDirector d,int first,PartyMiniGame game=PartyMiniGame.Countdown,double now=2,int iteration=1)
    {Stage(d,now);typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.ItemPreparation,First=first,MiniGame=game,Iteration=iteration,PhaseAt=now,StartedAt=now+5,ParticipantsMask=d.Game.AliveMask});}
    static void Rematch(DuelDirector d,double now)
    {
        d.Game.FinishParty(1);typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Idle,now});
        for(int bot=1;bot<6;bot++)if(RouletteRules.Has(d.Game.ConnectedMask,bot))d.Ready(bot,now);d.Ready(0,now);
    }
    static void Main()
    {
        foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})for(int count=1;count<=5;count++)
        {
            var d=Party(count,difficulty);var weapons=new HashSet<DuelWeapon>();int union=0;
            Check(d.Stage==DuelStage.Countdown&&d.Difficulty==difficulty,"all supported AI counts and difficulties start");
            for(int bot=1;bot<=count;bot++)
            {
                var a=d.ForSeat(bot);weapons.Add(a.SelectedWeapon);union|=a.SelectedItems;
                Check(WeaponRules.IsSelectable(a.SelectedWeapon)&&a.Weapon==a.SelectedWeapon&&DuelLoadout.Count(a.SelectedItems)==2,"bot has valid chosen gun and two distinct items");
                Check(a.HasRecovery==DuelLoadout.Has(a.SelectedItems,DuelItem.Recovery)&&a.HasTaser==DuelLoadout.Has(a.SelectedItems,DuelItem.Taser)&&a.HasNoruFace==DuelLoadout.Has(a.SelectedItems,DuelItem.NoruFace)&&a.Ink.Owned==DuelLoadout.Has(a.SelectedItems,DuelItem.Ink)&&a.BulletItems==(DuelLoadout.Has(a.SelectedItems,DuelItem.Bullet)?2:0),"only chosen inventory is granted");
            }
            Check(weapons.Count==Math.Min(count,3),"weapon variety across available bots");
            if(count==5)Check(union==DuelLoadout.All,"five-bot party covers all item types");
            Check(d.ForSeat(0).SelectedWeapon==DuelWeapon.BAS&&d.ForSeat(0).SelectedItems==DuelLoadout.Default,"human choices remain untouched");
        }
        var rotation=Party(1);var gunSet=new HashSet<DuelWeapon>();var pairSet=new HashSet<int>();int items=0;
        for(int round=1;round<=10;round++)
        {
            var a=rotation.ForSeat(1);gunSet.Add(a.SelectedWeapon);pairSet.Add(a.SelectedItems);items|=a.SelectedItems;
            int mask=a.SelectedItems;var gun=a.SelectedWeapon;typeof(DuelDirector).GetMethod("BeginPartySelection",Hidden).Invoke(rotation,new object[]{(round-1)*100d+1});
            Check(rotation.ForSeat(1).SelectedItems==mask&&rotation.ForSeat(1).SelectedWeapon==gun,"selection does not reroll between minigames");
            if(round<10)Rematch(rotation,round*100);
        }
        Check(gunSet.Count==3&&pairSet.Count==10&&items==DuelLoadout.All,"single bot rotates all weapons and all ten distinct item pairs on rematches");
        var quick=Party();Prep(quick,-1,PartyMiniGame.QuickHands);quick.Tick(2.8);
        Check(quick.ForSeat(2).QuickItem==2&&quick.ForSeat(3).QuickItem==3&&quick.ForSeat(5).NoruFaceActive,"quick-hands preparation distributes taser, ink and jacket");
        Check(quick.ForSeat(1).QuickItem==0&&!quick.ForSeat(1).BulletUsed,"healthy bot preserves healing and never uses bullet in quick hands");
        quick.Tick(7);quick.Tick(10);for(double time=10.02;time<=12;time+=.02)quick.Tick(time);
        Check(quick.ForSeat(2).FiredWeapon==DuelWeapon.Taser&&quick.ForSeat(2).ShotsFired>0,"bot equips and fires selected taser");
        Check(quick.ForSeat(3).Ink.Serial==1&&!quick.ForSeat(3).Ink.Owned&&!quick.ForSeat(3).QuickEquipped,"bot throws ink and returns to gun");
        var shared=Party();Prep(shared,1);shared.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body,true);shared.Tick(2.8);
        Check(shared.Game.HealthForSeat(1)==100&&!shared.ForSeat(1).HasRecovery&&shared.ForSeat(1).BulletUsed,"wounded shooter heals and reserves extra shot");
        Check(shared.ForSeat(2).TaserArmed&&shared.ForSeat(2).NoruFaceActive&&shared.ForSeat(3).Ink.Armed&&shared.ForSeat(5).NoruFaceActive,"non-shooters prepare offensive and defensive items");
        shared.Tick(7);for(double time=7.02;time<=9;time+=.02)shared.Tick(time);
        Check(shared.ForSeat(1).ShotsFired==2&&shared.ForSeat(1).BulletItems==1,"AI uses both shared shots without duplicating bullet charge");
        Check(shared.ForSeat(3).Ink.Serial==1,"shared-turn bot actually throws prepared ink");
        var healing=Party();Prep(healing,-1,PartyMiniGame.QuickHands);healing.Game.Shoot(0,1,DuelWeapon.Revolver,DuelHitPart.Body,true);healing.Tick(2.8);
        Check(healing.Game.HealthForSeat(1)==100&&healing.ForSeat(1).QuickSpent&&!healing.ForSeat(1).HasRecovery,"low-health bot prioritizes healing and obeys one-item slot");
        var infinite=Party(1,AiDifficulty.Normal,true,true);Prep(infinite,1);infinite.Tick(2.8);infinite.Tick(7);
        for(double time=7.02;time<12;time+=.02)infinite.Tick(time);
        Check(infinite.ForSeat(1).Ink.Serial==1&&infinite.ForSeat(1).ShotsFired==2,"unlimited ink is used once so bot resumes gun and extra shot");
        var switchBack=Party(2,AiDifficulty.Hard,true,true);Prep(switchBack,-1,PartyMiniGame.QuickHands);switchBack.Tick(2.8);switchBack.Tick(7);switchBack.Tick(10);
        int taserShots=0,lastShots=0;bool gunFired=false;
        for(double time=10.02;time<26;time+=.02)
        {
            switchBack.Tick(time);var a=switchBack.ForSeat(2);if(a.ShotsFired!=lastShots){lastShots=a.ShotsFired;if(a.FiredWeapon==DuelWeapon.Taser)taserShots++;else gunFired=true;}
            if(gunFired)break;
        }
        Check(taserShots==1&&gunFired,"unlimited taser bot switches back to its selected gun");
        for(int round=1;round<=3;round++)
        {
            var d=Party(1,AiDifficulty.Hard);for(int r=1;r<round;r++)Rematch(d,r*100);var weapon=d.ForSeat(1).SelectedWeapon;
            double start=(round-1)*100d+1;Stage(d,start);typeof(DuelClockContest).GetProperty("State").SetValue(Clock(d),new ClockState{Phase=ClockPhase.QuickHands,MiniGame=PartyMiniGame.QuickHands,Iteration=1,PhaseAt=start,StartedAt=start,ParticipantsMask=3});
            typeof(DuelDirector).GetMethod("ScheduleAi",Hidden).Invoke(d,new object[]{start});double time=start;
            while(d.ForSeat(1).ShotsFired==0&&time<start+40){time+=.02;d.Tick(time);}
            Check(d.ForSeat(1).ShotsFired>0&&d.ForSeat(1).FiredWeapon==weapon,"bot completes real numeric preparation and fires "+weapon);
            Check(d.ForSeat(1).CorrectInputs>=(weapon==DuelWeapon.Revolver?48:16),"weapon-specific input count respected "+weapon);
            Check(d.ForSeat(1).FiredAds==WeaponRules.SupportsAds(weapon),"gun-specific ADS respected "+weapon);
            d.Tick(time+.6);
            if(weapon==DuelWeapon.BAS)Check(d.ForSeat(1).LastHitPart==DuelHitPart.Head&&d.ForSeat(0).LivesRemaining==1,"sniper aims at head and applies its two-life effect");
        }
        var pvp=new DuelDirector(DuelMode.PvP);pvp.Join(0,0);pvp.Join(1,0);pvp.SetAutoSelectItems(true);pvp.SelectWeapon(1,DuelWeapon.BAS,0);pvp.Ready(0,0);pvp.Ready(1,0);
        Check(pvp.ForSeat(1).SelectedWeapon==DuelWeapon.BAS&&pvp.ForSeat(1).SelectedItems==DuelLoadout.All,"PvP players are not assigned AI presets");
        Console.WriteLine("PASS AI equipment: "+checks+" checks");
    }
}
