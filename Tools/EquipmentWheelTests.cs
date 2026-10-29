using System;
using HelloWorld;
class EquipmentWheelTests
{
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static DuelWheelSlot Next(DuelActionState a,bool practice,bool quick,bool shooting,int direction,double now=10,bool unlimited=false,int iteration=3)
        =>DuelEquipmentWheel.Next(a,practice,quick,shooting,unlimited,iteration,now,direction);
    static void Main()
    {
        var a=new DuelActionState{Ink=DuelInkState.Empty,SelectedItems=(1<<(int)DuelItem.Taser)|(1<<(int)DuelItem.Ink),HasTaser=true};a.Ink.Owned=true;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Taser,"practice wheel goes from gun to taser");
        Check(Next(a,true,false,false,-1)==DuelWheelSlot.Ink,"reverse wheel goes from gun to ink");
        a.QuickItem=2;a.QuickEquipped=true;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Ink&&Next(a,true,false,false,-1)==DuelWheelSlot.Weapon,"practice cycles all held equipment");
        a.QuickItem=3;a.Ink.Armed=true;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Weapon&&Next(a,true,false,false,-1)==DuelWheelSlot.Taser,"ink cycles to gun or taser");
        a.Ink.ThrownAt=9.9;a.Ink.ImpactAt=-1;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Weapon,"held flying ink can be holstered");
        a.QuickEquipped=false;a.Ink.Armed=false;
        Check(Next(a,true,false,false,1,10)==DuelWheelSlot.Weapon,"throw animation blocks re-equipping");
        Check(Next(a,true,false,false,1,10.7)==DuelWheelSlot.Taser,"taser available while pouch flies");
        Check(Next(a,true,false,false,-1,10.7)==DuelWheelSlot.Taser,"unresolved pouch skipped");
        a.Ink.ImpactAt=10.8;
        Check(Next(a,true,false,false,-1,11)==DuelWheelSlot.Ink,"pouch available after contact");
        a.SelectedItems=1<<(int)DuelItem.Recovery;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Weapon,"instant-use recovery is never activated by wheel");
        a.SelectedItems=DuelLoadout.All;a.QuickItem=2;a.QuickEquipped=false;a.QuickSpent=false;a.TaserReadyIteration=3;a.Ink=DuelInkState.Empty;a.Ink.Owned=true;
        Check(Next(a,false,true,false,1)==DuelWheelSlot.Taser&&Next(a,false,true,false,-1)==DuelWheelSlot.Taser,"quick hands toggles its one reserved item");
        a.TaserReadyIteration=4;
        Check(Next(a,false,true,false,1)==DuelWheelSlot.Weapon,"cooldown prevents equipping");
        Check(Next(a,false,true,false,1,10,true)==DuelWheelSlot.Taser,"cooldown setting permits equipping");
        a.QuickEquipped=true;a.QuickSpent=true;
        Check(Next(a,false,true,false,1)==DuelWheelSlot.Weapon,"used taser can be holstered");
        a.QuickEquipped=false;
        Check(Next(a,false,true,false,1,10,true)==DuelWheelSlot.Weapon,"spent item cannot be equipped again");
        a.QuickItem=3;a.QuickSpent=false;
        Check(Next(a,false,true,false,1)==DuelWheelSlot.Ink,"quick hands ink can be equipped");
        a.Ink.Armed=true;a.QuickEquipped=true;
        Check(Next(a,false,true,false,1)==DuelWheelSlot.Weapon,"quick hands ink can be holstered");
        a.QuickEquipped=false;a.Ink.Armed=false;
        Check(Next(a,false,false,true,1)==DuelWheelSlot.Weapon,"unselected ink cannot be used");
        a.PreparedItem=(int)DuelItem.Ink+1;
        Check(Next(a,false,false,true,1)==DuelWheelSlot.Ink,"shooting phase can hold ink");
        a.Ink.Armed=true;
        Check(Next(a,false,false,true,1)==DuelWheelSlot.Weapon,"shooting phase returns to base weapon");
        Check(Next(a,false,false,false,1)==DuelWheelSlot.Ink,"wheel does nothing outside allowed phases");
        a.Stunned=true;a.StunnedAt=9.5;
        Check(Next(a,true,false,false,1)==DuelWheelSlot.Ink,"stun blocks switching");
        Console.WriteLine("PASS equipment wheel: "+checks+" checks");
    }
}
