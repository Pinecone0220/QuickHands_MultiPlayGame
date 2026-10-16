using System;
using Unity.Collections;
using Unity.Netcode;

namespace HelloWorld
{
    public struct DuelDamageEntry : INetworkSerializable, IEquatable<DuelDamageEntry>
    {
        public FixedString64Bytes Attacker;
        public DuelWeapon Weapon;
        public DuelHitPart Part;
        public int Damage, Shot;
        public bool Splash, Fatal;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Attacker); s.SerializeValue(ref Weapon); s.SerializeValue(ref Part);
            s.SerializeValue(ref Damage); s.SerializeValue(ref Shot); s.SerializeValue(ref Splash); s.SerializeValue(ref Fatal);
        }
        public bool Equals(DuelDamageEntry o) => Attacker.Equals(o.Attacker) && Weapon == o.Weapon && Part == o.Part &&
            Damage == o.Damage && Shot == o.Shot && Splash == o.Splash && Fatal == o.Fatal;
    }

    // A single consumable heal permits at most 150 cumulative HP per life: thirty taser hits.
    public struct DuelDamageReport : INetworkSerializable, IEquatable<DuelDamageReport>
    {
        public const int Capacity=32;
        public int Round,Count,Life;
        public DuelDamageEntry Hit0;
        public DuelDamageEntry Hit1;
        public DuelDamageEntry Hit2;
        public DuelDamageEntry Hit3;
        public DuelDamageEntry Hit4;
        public DuelDamageEntry Hit5;
        public DuelDamageEntry Hit6;
        public DuelDamageEntry Hit7;
        public DuelDamageEntry Hit8;
        public DuelDamageEntry Hit9;
        public DuelDamageEntry Hit10;
        public DuelDamageEntry Hit11;
        public DuelDamageEntry Hit12;
        public DuelDamageEntry Hit13;
        public DuelDamageEntry Hit14;
        public DuelDamageEntry Hit15;
        public DuelDamageEntry Hit16;
        public DuelDamageEntry Hit17;
        public DuelDamageEntry Hit18;
        public DuelDamageEntry Hit19;
        public DuelDamageEntry Hit20;
        public DuelDamageEntry Hit21;
        public DuelDamageEntry Hit22;
        public DuelDamageEntry Hit23;
        public DuelDamageEntry Hit24;
        public DuelDamageEntry Hit25;
        public DuelDamageEntry Hit26;
        public DuelDamageEntry Hit27;
        public DuelDamageEntry Hit28;
        public DuelDamageEntry Hit29;
        public DuelDamageEntry Hit30;
        public DuelDamageEntry Hit31;
        public DuelDamageEntry ForHit(int index)
        {if(index<0||index>=Count)return default;switch(index){
            case 0:return Hit0;
            case 1:return Hit1;
            case 2:return Hit2;
            case 3:return Hit3;
            case 4:return Hit4;
            case 5:return Hit5;
            case 6:return Hit6;
            case 7:return Hit7;
            case 8:return Hit8;
            case 9:return Hit9;
            case 10:return Hit10;
            case 11:return Hit11;
            case 12:return Hit12;
            case 13:return Hit13;
            case 14:return Hit14;
            case 15:return Hit15;
            case 16:return Hit16;
            case 17:return Hit17;
            case 18:return Hit18;
            case 19:return Hit19;
            case 20:return Hit20;
            case 21:return Hit21;
            case 22:return Hit22;
            case 23:return Hit23;
            case 24:return Hit24;
            case 25:return Hit25;
            case 26:return Hit26;
            case 27:return Hit27;
            case 28:return Hit28;
            case 29:return Hit29;
            case 30:return Hit30;
            case 31:return Hit31;
            default:return default;}}
        public void Add(DuelDamageEntry entry)
        {if(entry.Damage<=0||Count>=Capacity)return;switch(Count){
            case 0:Hit0=entry;break;
            case 1:Hit1=entry;break;
            case 2:Hit2=entry;break;
            case 3:Hit3=entry;break;
            case 4:Hit4=entry;break;
            case 5:Hit5=entry;break;
            case 6:Hit6=entry;break;
            case 7:Hit7=entry;break;
            case 8:Hit8=entry;break;
            case 9:Hit9=entry;break;
            case 10:Hit10=entry;break;
            case 11:Hit11=entry;break;
            case 12:Hit12=entry;break;
            case 13:Hit13=entry;break;
            case 14:Hit14=entry;break;
            case 15:Hit15=entry;break;
            case 16:Hit16=entry;break;
            case 17:Hit17=entry;break;
            case 18:Hit18=entry;break;
            case 19:Hit19=entry;break;
            case 20:Hit20=entry;break;
            case 21:Hit21=entry;break;
            case 22:Hit22=entry;break;
            case 23:Hit23=entry;break;
            case 24:Hit24=entry;break;
            case 25:Hit25=entry;break;
            case 26:Hit26=entry;break;
            case 27:Hit27=entry;break;
            case 28:Hit28=entry;break;
            case 29:Hit29=entry;break;
            case 30:Hit30=entry;break;
            case 31:Hit31=entry;break;
        }Count++;}
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter
        {s.SerializeValue(ref Round);s.SerializeValue(ref Count);s.SerializeValue(ref Life);
            s.SerializeValue(ref Hit0);
            s.SerializeValue(ref Hit1);
            s.SerializeValue(ref Hit2);
            s.SerializeValue(ref Hit3);
            s.SerializeValue(ref Hit4);
            s.SerializeValue(ref Hit5);
            s.SerializeValue(ref Hit6);
            s.SerializeValue(ref Hit7);
            s.SerializeValue(ref Hit8);
            s.SerializeValue(ref Hit9);
            s.SerializeValue(ref Hit10);
            s.SerializeValue(ref Hit11);
            s.SerializeValue(ref Hit12);
            s.SerializeValue(ref Hit13);
            s.SerializeValue(ref Hit14);
            s.SerializeValue(ref Hit15);
            s.SerializeValue(ref Hit16);
            s.SerializeValue(ref Hit17);
            s.SerializeValue(ref Hit18);
            s.SerializeValue(ref Hit19);
            s.SerializeValue(ref Hit20);
            s.SerializeValue(ref Hit21);
            s.SerializeValue(ref Hit22);
            s.SerializeValue(ref Hit23);
            s.SerializeValue(ref Hit24);
            s.SerializeValue(ref Hit25);
            s.SerializeValue(ref Hit26);
            s.SerializeValue(ref Hit27);
            s.SerializeValue(ref Hit28);
            s.SerializeValue(ref Hit29);
            s.SerializeValue(ref Hit30);
            s.SerializeValue(ref Hit31);
        }
        public bool Equals(DuelDamageReport o)
        {if(Round!=o.Round||Count!=o.Count||Life!=o.Life)return false;for(int i=0;i<Count;i++)if(!ForHit(i).Equals(o.ForHit(i)))return false;return true;}
    }
}
