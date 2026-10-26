using System;
using System.Reflection;
using HelloWorld;
class LobbyBotsTests
{
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
    static DuelDirector Party()
    {
        var d=new DuelDirector(DuelMode.PvP,new Random(19),AiDifficulty.Hard);d.Join(0,0);d.Join(1,0);
        foreach(int seat in new[]{0,1}){d.SelectItem(seat,DuelItem.Recovery);d.SelectItem(seat,DuelItem.Taser);}d.Ready(0,0);
        for(int i=2;i<6;i++){Check(d.AddBot(i,1),"lobby bot joins");Check(d.IsBot(i)&&RouletteRules.Has(d.Game.ReadyMask,i),"bot auto ready");Check(DuelLoadout.Complete(d.ForSeat(i).SelectedItems),"bot chooses items");Check(RouletteRules.Has(d.Game.ReadyMask,0)&&!RouletteRules.Has(d.Game.ReadyMask,1),"existing human ready preserved");}
        return d;
    }
    static void State(DuelDirector d,ClockState s){typeof(DuelDirector).GetMethod("SetStage",Hidden).Invoke(d,new object[]{DuelStage.Racing,s.PhaseAt});var c=typeof(DuelDirector).GetField("clock",Hidden).GetValue(d);typeof(DuelClockContest).GetProperty("State").SetValue(c,s);}
    static void Main()
    {
        var d=Party();Check(!d.IsBot(0)&&!d.IsBot(1),"humans excluded from bot mask");Check(d.BotMask==60&&d.Game.ConnectedMask==63,"mixed room masks");Check(!d.AddBot(0,2)&&!d.AddBot(2,2)&&!d.AddBot(6,2),"invalid/full slot rejected");
        Check(d.Stage==DuelStage.Idle,"unready human blocks start");d.Game.SetShotTime(0);d.Game.SetManualMiniGame(true);d.Ready(1,2);Check(d.Game.Phase==DuelPhase.Playing,"all human ready starts mixed party");Check(!d.AddBot(5,3),"playing add rejected");
        State(d,new ClockState{Phase=ClockPhase.ItemPreparation,MiniGame=PartyMiniGame.QuickHands,ParticipantsMask=63,First=-1,Iteration=1,PhaseAt=3,StartedAt=8});d.Tick(3.8);
        Check(d.ForSeat(2).QuickItem==2&&d.ForSeat(3).QuickItem==3&&d.ForSeat(5).NoruFaceActive,"mixed-room bots use offensive and defensive items");Check(d.ForSeat(1).QuickItem==0,"human item untouched");
        d.Tick(8);d.Tick(11);bool fired=false,ink=false,taser=false;
        for(double t=11.02;t<38;t+=.02){d.Tick(t);for(int bot=2;bot<6;bot++){var a=d.ForSeat(bot);fired|=a.ShotsFired>0&&a.FiredWeapon!=DuelWeapon.Taser;taser|=a.ShotsFired>0&&a.FiredWeapon==DuelWeapon.Taser;ink|=a.Ink.Serial>0;}}
        Check(fired&&ink&&taser,"mixed-room bots enter commands, shoot and throw ink");Check(d.ForSeat(1).ShotsFired==0&&d.ForSeat(1).CorrectInputs==0&&d.ForSeat(1).Ink.Serial==0,"server never drives human controls");
        var left=new DuelDirector(DuelMode.PvP);left.Join(0,0);left.Join(1,0);left.AddBot(3,0);Check(left.Disconnect(3,1),"bot can leave");Check(!left.IsBot(3)&&left.BotMask==0,"departed bot identity cleared");left.Join(3,2);Check(!left.IsBot(3)&&left.ForSeat(3).SelectedItems==0&&!RouletteRules.Has(left.Game.ReadyMask,3),"reused human seat does not inherit AI");
        var lone=new DuelDirector(DuelMode.PvP);lone.Join(0,0);lone.SelectItem(0,DuelItem.Recovery);lone.SelectItem(0,DuelItem.Taser);lone.Ready(0,0);Check(lone.AddBot(1,1)&&lone.Game.Phase==DuelPhase.Playing,"ready solo host plus bot starts normally");
        foreach(var game in new[]{PartyMiniGame.Countdown,PartyMiniGame.Signal,PartyMiniGame.Greed,PartyMiniGame.Memory})
        {
            var mixed=Party();mixed.Game.SetManualMiniGame(true);mixed.Ready(1,2);mixed.Tick(4.6);Check(mixed.ChooseMiniGame(game,mixed.Clock.Iteration,4.7),"mixed room chooses "+game);
            var clock=(DuelClockContest)typeof(DuelDirector).GetField("clock",Hidden).GetValue(mixed);bool acted=false;
            if(game==PartyMiniGame.Greed)typeof(DuelClockContest).GetField("burstAt",Hidden).SetValue(clock,mixed.Clock.StartedAt+30);
            for(double t=4.72;t<45;t+=.02)
            {
                mixed.Tick(t);
                if(game==PartyMiniGame.Memory){if(mixed.MemoryProgress(2)>0){Check(mixed.MemoryProgress(1)==0,"human memory never automated");acted=true;break;}}
                else{int stopped=(int)typeof(DuelClockContest).GetField("stopped",Hidden).GetValue(clock);if((stopped&60)==60){Check((stopped&3)==0,"human timer/signal/greed never automated "+game);acted=true;break;}}
            }
            Check(acted,"mixed-room bots participate in "+game);
        }
        Console.WriteLine("PASS lobby bots: "+checks+" checks");
    }
}
