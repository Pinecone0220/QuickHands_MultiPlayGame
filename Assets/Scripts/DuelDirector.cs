using System;
namespace HelloWorld
{
    public enum DuelStage : byte { Idle, Countdown, Racing, AfterShot }
    public enum DuelAction : byte { Pickup, Load, Aim, Fire, OpenBarrel, CloseBarrel, UnlockBarrel, CockHammer }
    public enum DuelDeathCause : byte { None, Gunshot, ClockTimeout }
    public struct DuelActionState : IEquatable<DuelActionState>
    {
        public DuelAction Action;
        public DuelWeapon Weapon;
        public DuelWeapon SelectedWeapon,FiredWeapon;
        public int SelectedItems;
        public bool LoadoutLocked;
        public int Health;
        public int Code, Progress, Version, Mistakes, Target, FiredTarget;
        public int Kills, CorrectInputs, CurrentStreak, BestStreak;
        public int DamageDealt, ShotsFired, BestMultiKill;
        public int PracticeShotsFired,PracticeSession;
        public int ShotSerial=>ShotsFired+PracticeShotsFired;
        public int ClockWins, ClockStops, ClockErrorTotal;
        public int Life;
        public int LivesRemaining;
        public double ReviveAt;
        public bool HasRecovery,HasTaser,TaserArmed,Stunned;
        public int BulletItems;
        public bool BulletUsed;
        public bool HasNoruFace,NoruFaceActive;
        public int NoruFaceReadyIteration;
        public int TaserReadyIteration;
        public int QuickItem,QuickItemVersion;
        public bool QuickEquipped,QuickSpent;
        public double HealAt,StunnedAt;
        public bool IsStunned(double now)=>Stunned&&StunnedAt>=0&&now-StunnedAt<DuelDirector.TaserStunDuration;
        public DuelInkState Ink;
        public int LoadedMask,SpentMask,NextChamber;
        public int FiredBottleMask,FiredGlassMask;
        public int Ammo=>RouletteRules.Count(LoadedMask);
        public float AimYaw, AimPitch;
        public float FiredYaw, FiredPitch, FiredSpread;
        public int FiredAliveMask;
        public int FiredSeed;
        public bool FiredAds;
        public AimVector FiredOrigin;
        public bool Aiming;
        public double AimStartedAt,ShootingStartedAt;
        public DuelHitPart LastHitPart;
        public DuelDeathCause DeathCause;
        public double StartedAt, BlockedUntil, DiedAt, FiredAt, HitAt;
        public const int InputLength = 6;
        public int Length => WeaponRules.InputLength(Weapon,Action);
        public int Digit(int index)
        {
            if (index < 0 || index >= Length) return 0;
            int value = Code; for (int i = Length - 1; i > index; i--) value /= 10;
            return value % 10;
        }
        public bool Equals(DuelActionState o) => Action == o.Action && Code == o.Code && Progress == o.Progress &&
            Version == o.Version && Mistakes == o.Mistakes && Target == o.Target && LoadedMask==o.LoadedMask && SpentMask==o.SpentMask && NextChamber==o.NextChamber && FiredBottleMask==o.FiredBottleMask&&FiredGlassMask==o.FiredGlassMask&&
            StartedAt == o.StartedAt && BlockedUntil == o.BlockedUntil && DiedAt == o.DiedAt &&
            FiredAt == o.FiredAt && FiredTarget == o.FiredTarget && HitAt == o.HitAt && Weapon == o.Weapon && FiredWeapon==o.FiredWeapon && Health == o.Health &&
            FiredYaw==o.FiredYaw && FiredPitch==o.FiredPitch && FiredSpread==o.FiredSpread && FiredAliveMask==o.FiredAliveMask &&
            FiredSeed==o.FiredSeed && FiredAds==o.FiredAds && FiredOrigin.X==o.FiredOrigin.X && FiredOrigin.Y==o.FiredOrigin.Y && FiredOrigin.Z==o.FiredOrigin.Z &&
            Kills == o.Kills && CorrectInputs == o.CorrectInputs && CurrentStreak == o.CurrentStreak && BestStreak == o.BestStreak &&
            DamageDealt == o.DamageDealt && ShotsFired == o.ShotsFired && PracticeShotsFired==o.PracticeShotsFired && PracticeSession==o.PracticeSession && BestMultiKill == o.BestMultiKill &&
            AimYaw == o.AimYaw && AimPitch == o.AimPitch && Aiming == o.Aiming && AimStartedAt == o.AimStartedAt && ShootingStartedAt==o.ShootingStartedAt && LastHitPart == o.LastHitPart &&
            ClockWins == o.ClockWins && ClockStops == o.ClockStops && ClockErrorTotal == o.ClockErrorTotal && DeathCause==o.DeathCause && SelectedWeapon==o.SelectedWeapon && SelectedItems==o.SelectedItems && LoadoutLocked==o.LoadoutLocked && Life==o.Life && LivesRemaining==o.LivesRemaining && ReviveAt==o.ReviveAt && HasRecovery==o.HasRecovery && HasTaser==o.HasTaser && TaserArmed==o.TaserArmed && Stunned==o.Stunned && TaserReadyIteration==o.TaserReadyIteration && HealAt==o.HealAt && StunnedAt==o.StunnedAt && Ink.Equals(o.Ink)&&QuickItem==o.QuickItem&&QuickItemVersion==o.QuickItemVersion&&QuickEquipped==o.QuickEquipped&&QuickSpent==o.QuickSpent&&BulletItems==o.BulletItems&&BulletUsed==o.BulletUsed&&HasNoruFace==o.HasNoruFace&&NoruFaceActive==o.NoruFaceActive&&NoruFaceReadyIteration==o.NoruFaceReadyIteration;
    }
    public sealed partial class DuelDirector
    {
        public const double CountdownDuration = 2.5, AfterShotDuration = 1.65, MistakePenalty = 0.45;
        public const double RecoveryDuration=1.3;
        public const double TaserStunDuration=2;
        public RouletteRules Game { get; }
        public AiDifficulty Difficulty { get; }
        public int BotMask {get;private set;}
        public bool IsBot(int seat)=>RouletteRules.Has(BotMask&Game.ConnectedMask,seat);
        public bool AddBot(int seat,double now)=>seat>0&&seat<RouletteRules.MaxPlayers&&Join(seat,now,true);
        private readonly DuelClockContest clock;
        private readonly DuelTabletop tabletop=new DuelTabletop();
        public DuelTabletopState Tabletop=>tabletop.State;
        public ClockState Clock => clock.PublicState();
        public ClockEntry SubmittedEntry(int seat)=>clock.State.ForSeat(seat);
        public bool QuickHandsActive=>clock.State.Phase==ClockPhase.QuickHands;
        public int PartyAliveMask { get; private set; }
        public DuelStage Stage { get; private set; }
        public double StartedAt { get; private set; }
        public double RaceStartedAt { get; private set; }
        public int RoundParticipantsMask { get; private set; }
        public int Sequence { get; private set; }
        public event Action ShotConfirmed;
        private readonly DuelActionState[] actions = new DuelActionState[RouletteRules.MaxPlayers];
        private readonly DuelWeapon[] selectedWeapons=new DuelWeapon[RouletteRules.MaxPlayers];
        private readonly int[] selectedItems=new int[RouletteRules.MaxPlayers];
        private readonly DuelProjectileVolley[] projectiles=new DuelProjectileVolley[RouletteRules.MaxPlayers];
        public DuelActionState ForSeat(int seat)
        {
            if (seat < 0 || seat >= RouletteRules.MaxPlayers) return default;
            var state = actions[seat]; state.Health = Game.HealthForSeat(seat);state.SelectedWeapon=selectedWeapons[seat];state.SelectedItems=selectedItems[seat];
            if(IsPracticing(seat))
            {
                state.Weapon=selectedWeapons[seat];
                bool lever=state.Weapon==DuelWeapon.BAS&&state.Action==DuelAction.OpenBarrel;
                state.Action=lever?DuelAction.OpenBarrel:DuelAction.Aim;
                state.LoadedMask=lever?0:(1<<WeaponRules.Capacity(state.Weapon))-1;state.SpentMask=lever?1:0;
                state.Health=RouletteRules.MaxHealth;state.LivesRemaining=RouletteRules.StartingLives;
                state.DiedAt=state.ReviveAt=-1;state.DeathCause=DuelDeathCause.None;
            }
            return state;
        }
        private readonly Random random;
        private readonly double[] nextAi = new double[RouletteRules.MaxPlayers];
        private readonly double[] lastLook = new double[RouletteRules.MaxPlayers];
        private bool shotOpportunitySpent;
        private readonly double[] nextShockShot=new double[RouletteRules.MaxPlayers];
        private readonly int[] aiTarget = new int[RouletteRules.MaxPlayers];
        public DuelDirector(DuelMode mode, Random random = null, AiDifficulty difficulty = AiDifficulty.Normal)
        {
            Game = new RouletteRules(mode); this.random = random ?? new Random();
            Difficulty = difficulty >= AiDifficulty.Easy && difficulty <= AiDifficulty.Hard ? difficulty : AiDifficulty.Normal;
            clock = new DuelClockContest(this.random);
            for (int i = 0; i < RouletteRules.MaxPlayers; i++) { actions[i].DiedAt = actions[i].FiredAt = actions[i].HitAt = -1; actions[i].Target = actions[i].FiredTarget = -1;actions[i].Ink=DuelInkState.Empty; }
        }
        private void SetStage(DuelStage value, double now) { Stage = value; StartedAt = now; Sequence++; }
        private void ResetAction(int seat, DuelAction action, double now)
        {
            var state = actions[seat]; state.Action = action;
            if(action==DuelAction.Pickup){state.LoadedMask=0;state.ShootingStartedAt=-1;}
            if(action==DuelAction.Aim)state.ShootingStartedAt=now;
            state.Code = 0; state.Progress = 0; state.Version++;
            state.Target = -1; state.Aiming = false;
            // One sequence per action; its length depends on the selected weapon.
            for (int i = 0; i < state.Length; i++) state.Code = state.Code * 10 + random.Next(1, 6);
            state.StartedAt = now; state.BlockedUntil = now; actions[seat] = state;
        }
        public bool Join(int seat, double now, bool bot=false)
        {
            if (Stage != DuelStage.Idle || !Game.Join(seat)) return false;
            if(bot||Game.Mode==DuelMode.AI&&seat>0)BotMask|=1<<seat;else BotMask&=~(1<<seat);
            InvalidatePracticeFlights();
            int practiceSession=actions[seat].PracticeSession+1;
            actions[seat] = new DuelActionState { Weapon = DuelWeapon.Revolver,LivesRemaining=RouletteRules.StartingLives,ReviveAt=-1,PracticeSession=practiceSession,Ink=DuelInkState.Empty };
            tabletop.Joined(seat,now);
            selectedWeapons[seat]=DuelWeapon.Revolver;
            selectedItems[seat]=Game.AutoSelectItems?DuelLoadout.All:IsBot(seat)?DuelLoadout.AiSelection(seat):0;
            ConfigureBotLoadout(seat,Game.Round+1);
            actions[seat].AimYaw = seat * 60;
            actions[seat].DiedAt = actions[seat].FiredAt = actions[seat].HitAt = -1; actions[seat].FiredTarget = -1;
            ResetAction(seat, DuelAction.Pickup, now); Sequence++;
            for (int ai = 1; ai < RouletteRules.MaxPlayers; ai++) if (IsBot(ai)) Game.SetReady(ai);
            TryStart(now);
            return true;
        }
        public bool Ready(int seat, double now)
        {
            if(Stage!=DuelStage.Idle)return false;
            if(RouletteRules.Has(Game.ReadyMask,seat))
            {
                if(!Game.CancelReady(seat))return false;
                ChangePracticeSession(seat,now);
                Sequence++;return true;
            }
            if (!DuelLoadout.Complete(ForSeat(seat).SelectedItems) || !Game.SetReady(seat)) return false;
            ChangePracticeSession(seat,now);
            Sequence++; TryStart(now); return true;
        }
        public bool SelectWeapon(int seat, DuelWeapon weapon, double now)
        {
            if (Stage != DuelStage.Idle || Game.Phase == DuelPhase.Playing || !WeaponRules.IsSelectable(weapon) || !RouletteRules.Has(Game.ConnectedMask, seat) ||
                RouletteRules.Has(Game.ReadyMask, seat) || selectedWeapons[seat] == weapon) return false;
            selectedWeapons[seat]=weapon;
            actions[seat].Weapon = weapon; ResetAction(seat, DuelAction.Pickup, now); Sequence++; return true;
        }
        public bool SelectItem(int seat,DuelItem item)
        {
            if(Stage!=DuelStage.Idle||Game.Phase==DuelPhase.Playing||Game.AutoSelectItems||!DuelLoadout.Selectable(item)||ForSeat(seat).LoadoutLocked||
                !RouletteRules.Has(Game.ConnectedMask,seat)||RouletteRules.Has(Game.ReadyMask,seat))return false;
            int next=selectedItems[seat]^(1<<(int)item);
            if(DuelLoadout.Count(next)>DuelLoadout.Required)return false;
            selectedItems[seat]=next;Sequence++;return true;
        }
        public bool SetAutoSelectItems(bool enabled)
        {
            if(Stage!=DuelStage.Idle||!Game.SetAutoSelectItems(enabled))return false;
            if(enabled)for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                if(RouletteRules.Has(Game.ConnectedMask,seat))selectedItems[seat]=DuelLoadout.All;
            Sequence++;return true;
        }
        private void TryStart(double now)
        {
            for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                if(RouletteRules.Has(Game.ConnectedMask,seat)&&!DuelLoadout.Complete(selectedItems[seat]))return;
            if (!Game.Start()) return;
            for(int bot=1;bot<actions.Length;bot++)if(RouletteRules.Has(Game.ConnectedMask,bot))ConfigureBotLoadout(bot,Game.Round);
            practiceProjectiles.Clear();
            tabletop.Reset(now);
            Array.Clear(projectiles,0,projectiles.Length);
            Array.Clear(inkFlights,0,inkFlights.Length);
            ResetQuickItems();
            RoundParticipantsMask = Game.ConnectedMask; RaceStartedAt = now;
            PartyAliveMask=Game.AliveMask;
            clock.Reset();
            for (int seat = 0; seat < RouletteRules.MaxPlayers; seat++)
            {
                actions[seat].DiedAt = actions[seat].FiredAt = actions[seat].HitAt = -1; actions[seat].FiredTarget = -1;
                actions[seat].Mistakes = 0; actions[seat].Target = -1;
                actions[seat].Kills = actions[seat].CorrectInputs = actions[seat].CurrentStreak = actions[seat].BestStreak = 0;
                actions[seat].DamageDealt = actions[seat].ShotsFired = actions[seat].BestMultiKill = 0;
                actions[seat].PracticeShotsFired=0;actions[seat].PracticeSession++;
                actions[seat].ClockWins = actions[seat].ClockStops = actions[seat].ClockErrorTotal = 0;
                actions[seat].HasRecovery=RouletteRules.Has(Game.ConnectedMask,seat)&&DuelLoadout.Has(selectedItems[seat],DuelItem.Recovery);
                actions[seat].HasTaser=RouletteRules.Has(Game.ConnectedMask,seat)&&DuelLoadout.Has(selectedItems[seat],DuelItem.Taser);
                actions[seat].BulletItems=RouletteRules.Has(Game.ConnectedMask,seat)&&DuelLoadout.Has(selectedItems[seat],DuelItem.Bullet)?2:0;
                actions[seat].BulletUsed=false;
                actions[seat].HasNoruFace=RouletteRules.Has(Game.ConnectedMask,seat)&&DuelLoadout.Has(selectedItems[seat],DuelItem.NoruFace);
                actions[seat].NoruFaceActive=false;actions[seat].NoruFaceReadyIteration=0;
                actions[seat].Ink=DuelInkState.Empty;actions[seat].Ink.Owned=RouletteRules.Has(Game.ConnectedMask,seat)&&DuelLoadout.Has(selectedItems[seat],DuelItem.Ink);
                actions[seat].LoadoutLocked=RouletteRules.Has(Game.ConnectedMask,seat);
                actions[seat].TaserReadyIteration=0;actions[seat].TaserArmed=actions[seat].Stunned=false;actions[seat].HealAt=actions[seat].StunnedAt=-1;
                actions[seat].DeathCause=DuelDeathCause.None;
                actions[seat].Life=1;
                actions[seat].LivesRemaining=RouletteRules.StartingLives;actions[seat].ReviveAt=-1;
                actions[seat].LoadedMask=actions[seat].SpentMask=actions[seat].NextChamber=0;
                actions[seat].AimYaw = seat * 60; actions[seat].AimPitch = 0; actions[seat].Aiming = false;
                actions[seat].AimStartedAt = 0; actions[seat].LastHitPart = DuelHitPart.None;
                lastLook[seat] = now; aiTarget[seat] = -1;
                actions[seat].Weapon=selectedWeapons[seat];
                ResetAction(seat, DuelAction.Pickup, now);
            }
            SetStage(DuelStage.Countdown, now);
        }
        public bool ChooseMiniGame(PartyMiniGame game,int iteration,double now)
        {
            if(!Game.ManualMiniGame||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||iteration!=clock.State.Iteration||!clock.ChooseManual(game,now))return false;
            return TickClock(now,true);
        }
        public void ResetDeveloperSettings(double now)
        {
            bool restart=Game.ManualMiniGame&&Game.Phase==DuelPhase.Playing&&clock.State.Phase==ClockPhase.Selecting;
            if(Game.ResetDeveloperSettings())Sequence++;
            if(restart)BeginPartySelection(now);
        }
        public bool UseItem(int seat,DuelItem item,int iteration,double now)
        {
            if(item==DuelItem.NoruFace)return UseNoruFace(seat,iteration,now);
            if(item==DuelItem.Bullet)return UseBullet(seat,iteration,now);
            if(clock.State.MiniGame==PartyMiniGame.QuickHands)return SelectQuickItem(seat,item,iteration,now);
            if(item==DuelItem.Ink)return UseInk(seat,iteration,now);
            if(Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||clock.State.Phase!=ClockPhase.ItemPreparation||now>=clock.State.StartedAt||iteration!=clock.State.Iteration||!RouletteRules.Has(Game.AliveMask,seat))return false;
            var a=actions[seat];
            if(item==DuelItem.Recovery)
            {if(!a.HasRecovery||!Game.Heal(seat,50))return false;if(!Game.UnlimitedItems)a.HasRecovery=false;a.HealAt=now;}
            else if(item==DuelItem.Taser)
            {if(!a.HasTaser||a.TaserArmed||!Game.UnlimitedItems&&iteration<a.TaserReadyIteration||clock.State.MiniGame==PartyMiniGame.QuickHands||clock.State.First<0||seat==clock.State.First)return false;a.TaserArmed=true;a.TaserReadyIteration=iteration+3;}
            else return false;
            actions[seat]=a;Sequence++;return true;
        }
        private void ClearTurnEffects()
        {ClearQuickItems();ClearInkTurn();for(int i=0;i<actions.Length;i++){actions[i].NoruFaceActive=false;actions[i].TaserArmed=actions[i].Stunned=false;actions[i].StunnedAt=-1;if(actions[i].Weapon==DuelWeapon.Taser){actions[i].Weapon=selectedWeapons[i];actions[i].LoadedMask=0;actions[i].Aiming=false;actions[i].Target=-1;actions[i].Version++;}}}
        private bool AnyProjectile(){foreach(var p in projectiles)if(p!=null)return true;return AnyInkFlight();}
        private bool TickShock(double now)
        {
            bool changed=false;
            bool shooting=DuelInk.Shooting(clock.State.Phase)&&Game.Phase==DuelPhase.Playing;
            for(int i=0;i<actions.Length;i++)if(actions[i].Stunned&&
                (!shooting||!RouletteRules.Has(Game.AliveMask,i)||!actions[i].IsStunned(now)))
            {
                actions[i].Stunned=false;actions[i].StunnedAt=-1;nextShockShot[i]=double.PositiveInfinity;
                lastLook[i]=now-1;Sequence++;changed=true;
            }
            if(!shooting)return changed;
            for(int i=0;i<actions.Length;i++)if(actions[i].Stunned&&RouletteRules.Has(Game.AliveMask,i)&&now>=nextShockShot[i])
            {
                nextShockShot[i]=now+.18+random.NextDouble()*.42;
                bool armedShooter=QuickHandsActive||clock.State.First==i;
                var a=actions[i];if(armedShooter){a.AimYaw=(float)(random.NextDouble()*360);a.AimPitch=(float)(random.NextDouble()*70-35);}a.Target=-1;a.Aiming=false;actions[i]=a;lastLook[i]=now;
                if(armedShooter&&a.Ammo>0&&random.NextDouble()<.55)changed|=Fire(i,a.AimYaw,a.AimPitch,false,a.Version,now,now,true);
                changed=true;Sequence++;
            }
            return changed;
        }
        public bool Input(int seat, int digit, int version, double now)
        {
            if(IsPracticing(seat))return InputPracticeLever(seat,digit,version,now);
            if (!QuickHandsActive || !RouletteRules.Has(Game.AliveMask, seat) || digit < 1 || digit > 5 || Stage != DuelStage.Racing || Game.Phase != DuelPhase.Playing) return false;
            if (actions[seat].QuickEquipped||DuelInk.Busy(actions[seat].Ink,now)||actions[seat].Action == DuelAction.Aim || actions[seat].Action == DuelAction.Fire) return false;
            var state = actions[seat]; if (version != state.Version || now < state.BlockedUntil) return false;
            state.Version++;
            if (digit != state.Digit(state.Progress))
            {
                state.Progress = 0; state.Mistakes++; state.BlockedUntil = now + MistakePenalty;
                state.CurrentStreak = 0;
                if (state.Weapon == DuelWeapon.Revolver)
                {
                    int code = 0;
                    for (int i = 0; i < state.Length; i++)
                    {
                        // Choose among the other four digits so every position actually changes.
                        int next = random.Next(1, 5);
                        if (next >= state.Digit(i)) next++;
                        code = code * 10 + next;
                    }
                    state.Code = code; state.StartedAt = now;
                }
            }
            else
            {
                state.Progress++; state.CorrectInputs++; state.CurrentStreak++;
                state.BestStreak = Math.Max(state.BestStreak, state.CurrentStreak);
            }
            actions[seat] = state; Sequence++;
            if (state.Progress == state.Length)
            {
                var next=WeaponRules.NextAction(state.Weapon,state.Action);
                if(state.Weapon==DuelWeapon.Revolver&&state.Action==DuelAction.CloseBarrel || (state.Weapon==DuelWeapon.SawedOff||state.Weapon==DuelWeapon.BAS)&&state.Action==DuelAction.OpenBarrel)
                {actions[seat].SpentMask=0;actions[seat].ShootingStartedAt=-1;}
                if(state.Action==DuelAction.Load)
                {
                    int capacity=WeaponRules.Capacity(state.Weapon),mask=(1<<capacity)-1;
                    int chamber=WeaponRules.Chamber(mask&~(actions[seat].LoadedMask|actions[seat].SpentMask),state.NextChamber);
                    if(chamber>=0){actions[seat].LoadedMask|=1<<chamber;actions[seat].SpentMask&=~(1<<chamber);}
                    if(state.Weapon==DuelWeapon.Revolver&&actions[seat].Ammo<capacity)next=DuelAction.Load;
                }
                ResetAction(seat,next,now);
            }
            return true;
        }
        public bool Look(int seat,float yaw,float pitch,bool ads,double now)
        {
            if(!DuelGunplay.ValidSeatAngles(seat,yaw,pitch))return false;
            bool lobby=Stage==DuelStage.Idle&&Game.Phase==DuelPhase.Waiting&&RouletteRules.Has(Game.ConnectedMask,seat);
            bool countdown=Stage==DuelStage.Countdown&&Game.Phase==DuelPhase.Playing&&RouletteRules.Has(Game.AliveMask,seat);
            if ((!lobby&&!countdown&&!IsPracticing(seat)&&(Stage != DuelStage.Racing || Game.Phase != DuelPhase.Playing || !RouletteRules.Has(Game.AliveMask,seat))) || actions[seat].IsStunned(now)) return false;
            if(countdown||lobby&&!IsPracticing(seat))ads=false;
            ads&=WeaponRules.SupportsAds(actions[seat].Weapon);
            if (now-lastLook[seat] < (ads==actions[seat].Aiming ? 1.0/30 : .01)) return false;
            ApplyLook(seat,yaw,pitch,ads,now); return true;
        }
        private void ApplyLook(int seat,float yaw,float pitch,bool ads,double now)
        {
            var a = actions[seat];
            bool practice=IsPracticing(seat);
            ads&=WeaponRules.SupportsAds(a.Weapon);
            ads&=!DuelInk.Busy(a.Ink,now);
            ads &= practice ? a.Weapon!=DuelWeapon.BAS||a.Action==DuelAction.Aim : (a.Action==DuelAction.Aim || a.Weapon==DuelWeapon.Revolver) && (QuickHandsActive || CanClockShoot(seat,now));
            if (ads && !a.Aiming) a.AimStartedAt = now;
            if(ads && a.Ammo>0 && a.ShootingStartedAt<0)a.ShootingStartedAt=now;
            a.Aiming = ads; a.AimYaw = yaw; a.AimPitch = pitch; lastLook[seat] = now;
            a.Target = -1;
            if (ads && (practice||a.Ammo>0))
            {
                DuelGunplay.Sway(seat,a.Weapon,true,now,a.AimStartedAt,out float sy,out float sp);
                var centre=DuelGunplay.Direction(yaw+sy,pitch+sp);
                int targets=practice?PracticeMask:Game.AliveMask;
                a.Target = (a.Weapon==DuelWeapon.SawedOff?DuelGunplay.TraceShotgun(seat,targets,centre,DuelGunplay.Spread(a.Weapon,true),0,tabletop.State):DuelGunplay.Trace(seat,targets,centre,tabletop.State)).Seat;
            }
            actions[seat] = a; Sequence++;
        }
        public bool Fire(int seat,float yaw,float pitch,bool ads,int version,double shotTime,double now,bool involuntary=false)
        {
            if(seat<0||seat>=actions.Length)return false;
            if(DuelInk.Busy(actions[seat].Ink,now))return false;
            ads&=WeaponRules.SupportsAds(actions[seat].Weapon);
            if(IsPracticing(seat))return !involuntary&&FirePractice(seat,yaw,pitch,ads,version,shotTime,now);
            if (Stage != DuelStage.Racing || Game.Phase != DuelPhase.Playing || RouletteRules.Count(Game.AliveMask)<2 || !RouletteRules.Has(Game.AliveMask,seat) ||
                (!QuickHandsActive && !CanClockShoot(seat,now)) || actions[seat].IsStunned(now)&&!involuntary ||
                (actions[seat].Action!=DuelAction.Aim && !(actions[seat].Weapon==DuelWeapon.Revolver&&(ads||actions[seat].SpentMask!=0))) || actions[seat].Ammo<=0 || projectiles[seat]!=null || actions[seat].Version != version || !(involuntary?DuelGunplay.ValidAngles(yaw,pitch):DuelGunplay.ValidSeatAngles(seat,yaw,pitch)) ||
                double.IsNaN(shotTime) || double.IsInfinity(shotTime) || shotTime < now-.3 || shotTime > now+.05) return false;
            if(QuickHandsActive && Game.ShotTimeLimit>0 && actions[seat].ShootingStartedAt>=0 && now-actions[seat].ShootingStartedAt>=Game.ShotTimeLimit)return false;
            ApplyLook(seat,yaw,pitch,ads,now);
            var a = actions[seat];
            DuelGunplay.Sway(seat,a.Weapon,ads,shotTime,a.AimStartedAt,out float sy,out float sp);
            float firedYaw=yaw+sy,firedPitch=pitch+sp;
            var bore=DuelGunplay.Direction(firedYaw,firedPitch);
            float spread=DuelGunplay.Spread(a.Weapon,ads);
            if(WeaponRules.IsPrecision(a.Weapon))
            {
                double angle=random.NextDouble()*Math.PI*2;
                float radius=DuelGunplay.PrecisionSpreadRadius(a.Weapon,ads,random.NextDouble());
                firedYaw+=(float)Math.Cos(angle)*radius;firedPitch+=(float)Math.Sin(angle)*radius;
                spread=0;
            }
            var ray = DuelGunplay.Direction(firedYaw,firedPitch);
            int seed=a.Weapon==DuelWeapon.SawedOff?random.Next(int.MaxValue):0;
            var origin=DuelGunplay.MuzzleOrigin(seat,a.Weapon,ads,bore);
            projectiles[seat]=new DuelProjectileVolley(seat,a.Life,clock.State.Iteration,a.Weapon,origin,ray,spread,seed,now);
            actions[seat].ShotsFired++;
            actions[seat].FiredWeapon=a.Weapon;actions[seat].FiredAt = now; actions[seat].FiredTarget = -1;
            actions[seat].FiredYaw=firedYaw;actions[seat].FiredPitch=firedPitch;
            actions[seat].FiredSpread=spread;actions[seat].FiredAliveMask=Game.AliveMask;
            actions[seat].FiredSeed=seed;actions[seat].FiredOrigin=origin;actions[seat].FiredAds=ads;
            actions[seat].FiredBottleMask=tabletop.State.BottlesBrokenMask;actions[seat].FiredGlassMask=tabletop.State.GlassesBrokenMask;
            int firedChamber=WeaponRules.Chamber(actions[seat].LoadedMask,actions[seat].NextChamber);
            actions[seat].LoadedMask&=~(1<<firedChamber);actions[seat].SpentMask|=1<<firedChamber;
            actions[seat].NextChamber=(firedChamber+1)%WeaponRules.Capacity(a.Weapon);
            ResetAction(seat,WeaponRules.ReloadAction(a.Weapon),now);
            if(!QuickHandsActive&&a.Weapon!=DuelWeapon.Taser)clockShotsRemaining--;
            actions[seat].ShootingStartedAt=actions[seat].Ammo>0?now:-1;
            if(quickTaser(seat)){actions[seat].QuickSpent=!Game.UnlimitedItems;actions[seat].QuickItemVersion++;actions[seat].TaserReadyIteration=Game.UnlimitedItems?0:clock.State.Iteration+3;actions[seat].Action=DuelAction.Aim;}
            aiTarget[seat]=-1;Retarget();Sequence++;
            return true;
        }
        private void ResolveProjectile(DuelProjectileVolley volley,DuelRayHit hit,double now)
        {
            int seat=volley.Actor;bool quick=QuickHandsActive;
            if(volley.Weapon==DuelWeapon.Taser&&hit.Seat>=0&&actions[hit.Seat].NoruFaceActive)
                hit=new DuelRayHit{Seat=-1,Part=DuelHitPart.None};
            if(!RouletteRules.Has(Game.AliveMask,hit.Seat))hit=new DuelRayHit{Seat=-1,Part=DuelHitPart.None};
            if (!Game.Shoot(seat,hit.Seat,volley.Weapon,hit.Part,true)) return;
            int kills = RouletteRules.Count(Game.LastKilledMask);
            actions[seat].Kills += kills;
            actions[seat].DamageDealt += Game.LastDamage;
            actions[seat].BestMultiKill = Math.Max(actions[seat].BestMultiKill,kills);
            actions[seat].LastHitPart = hit.Part;
            for (int victim=0;victim<RouletteRules.MaxPlayers;victim++)
            {
                if (RouletteRules.Has(Game.LastHitMask,victim)) actions[victim].HitAt = now;
                if (RouletteRules.Has(Game.LastKilledMask,victim)) LoseLife(victim,DuelDeathCause.Gunshot,now,WeaponRules.LivesLost(volley.Weapon,hit.Part));
            }
            actions[seat].FiredTarget = hit.Seat;
            Sequence++;
            if(quick)
            {clock.RecordQuickDeaths(Game.LastKilledMask,RouletteRules.Count(Game.AliveMask));}
            if(volley.Weapon==DuelWeapon.Taser&&hit.Seat>=0&&RouletteRules.Has(Game.AliveMask,hit.Seat)&&DuelInk.Shooting(clock.State.Phase))
            {HolsterQuickItem(hit.Seat);actions[hit.Seat].Stunned=true;actions[hit.Seat].StunnedAt=now;actions[hit.Seat].Aiming=false;actions[hit.Seat].Target=-1;if(actions[hit.Seat].Ink.Armed){actions[hit.Seat].Ink.Armed=false;actions[hit.Seat].Ink.Version++;}nextShockShot[hit.Seat]=now+.12+random.NextDouble()*.18;}
            if (!quick && volley.Weapon!=DuelWeapon.Taser)
            {
                shotOpportunitySpent=clockShotsRemaining<=0;
                if(!shotOpportunitySpent&&RouletteRules.Has(Game.AliveMask,seat)&&RouletteRules.Count(Game.AliveMask)>1)
                    PrepareBonusShot(seat,now);
            }
            Retarget();ShotConfirmed?.Invoke();
        }
        private bool TickProjectiles(double now)
        {
            var propsBefore=tabletop.State;
            var hits=new DuelRayHit[RouletteRules.MaxPlayers];var ready=new bool[RouletteRules.MaxPlayers];bool changed=false;
            // Sweep all contacts before applying damage so same-tick projectiles can trade kills.
            for(int seat=0;seat<projectiles.Length;seat++)
            {
                var v=projectiles[seat];if(v==null)continue;
                if(Game.Phase!=DuelPhase.Playing || !QuickHandsActive&&clock.State.Phase!=ClockPhase.Shooting || !RouletteRules.Has(Game.ConnectedMask,seat) || v.Life!=actions[seat].Life || v.Iteration!=clock.State.Iteration)
                {projectiles[seat]=null;continue;}
                ready[seat]=v.Tick(Game.AliveMask,now,out hits[seat],tabletop);
            }
            for(int seat=0;seat<projectiles.Length;seat++)if(ready[seat])
            {var v=projectiles[seat];projectiles[seat]=null;ResolveProjectile(v,hits[seat],now);changed=true;}
            if(changed)
            {
                bool inFlight=clock.State.Phase==ClockPhase.Shooting&&AnyInkFlight();foreach(var v in projectiles)if(v!=null){inFlight=true;break;}
                // Let bullets already fired finish before declaring the last survivor.
                if(!inFlight)
                {
                    if(!FinishPartyIfReady(now)&&QuickHandsActive&&RouletteRules.Count(Game.AliveMask)<=1)FinishQuick(now);
                    if(!QuickHandsActive&&shotOpportunitySpent&&clock.State.Phase==ClockPhase.Shooting)clock.Return(now);
                }
            }
            if(!propsBefore.Equals(tabletop.State)){Sequence++;changed=true;}
            return changed;
        }
        public double AiInterval(double sample) => Difficulty == AiDifficulty.Easy ? 0.65 + sample * 0.25 :
            Difficulty == AiDifficulty.Hard ? 0.16 + sample * 0.08 : 0.32 + sample * 0.14;
        public double AiErrorChance => Difficulty == AiDifficulty.Easy ? 0.18 : Difficulty == AiDifficulty.Hard ? 0.02 : 0.08;
        public bool Tick(double now)
        {
            bool changed = false;
            changed|=TickPractice(now);
            bool idleProps=Game.Phase==DuelPhase.Waiting||Game.Phase==DuelPhase.Playing&&Stage==DuelStage.Racing&&
                (clock.State.Phase==ClockPhase.Selecting||clock.State.Phase==ClockPhase.Counting||clock.State.Phase==ClockPhase.Ranking||clock.State.Phase==ClockPhase.Memory);
            if(tabletop.Tick((Game.Phase==DuelPhase.Playing?Game.AliveMask:Game.ConnectedMask)&~PracticeMask,idleProps,now)){Sequence++;changed=true;}
            changed |= TickProjectiles(now);
            changed |= TickInk(now);changed|=TickInkAi(now);
            changed|=TickQuickItemAi(now);
            changed |= TickShock(now);
            if((Game.Phase!=DuelPhase.Playing||clock.State.Phase!=ClockPhase.Shooting&&clock.State.Phase!=ClockPhase.ItemPreparation&&clock.State.Phase!=ClockPhase.QuickCountdown&&clock.State.Phase!=ClockPhase.QuickHands)&&HasTurnEffects()){ClearTurnEffects();Sequence++;changed=true;}
            if(Game.Phase==DuelPhase.Playing&&Stage==DuelStage.Racing&&QuickHandsActive&&Game.ShotTimeLimit>0)
                for(int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                    if(RouletteRules.Has(Game.AliveMask,seat)&&!quickTaser(seat)&&actions[seat].ShootingStartedAt>=0&&now-actions[seat].ShootingStartedAt>=Game.ShotTimeLimit)
                    {ResetAction(seat,DuelAction.Pickup,now);nextAi[seat]=now+AiInterval(.5);Sequence++;changed=true;}
            if (Stage == DuelStage.Countdown && now - StartedAt >= CountdownDuration)
            {
                RaceStartedAt = now;
                for (int seat = 0; seat < RouletteRules.MaxPlayers; seat++) actions[seat].StartedAt = now;
                SetStage(DuelStage.Racing, now);
                BeginPartySelection(now);
                changed = true;
            }
            else if (Stage == DuelStage.AfterShot && now - StartedAt >= AfterShotDuration)
            {
                SetStage(DuelStage.Idle, now);
                changed = true;
            }
            if(Stage==DuelStage.Racing && Game.Phase==DuelPhase.Playing)
                changed |= TickClock(now);
            for (int bot = 1; bot < RouletteRules.MaxPlayers; bot++)
            {
                if(!IsBot(bot))continue;
                if (Stage == DuelStage.Idle && RouletteRules.Has(Game.ConnectedMask, bot) && !RouletteRules.Has(Game.ReadyMask, bot))
                    changed |= Ready(bot, now);
                if(!QuickHandsActive||actions[bot].QuickEquipped) continue;
                if (Stage != DuelStage.Racing || !RouletteRules.Has(Game.AliveMask, bot) || now < nextAi[bot]) continue;
                if (actions[bot].Action == DuelAction.Aim || actions[bot].Weapon==DuelWeapon.Revolver&&actions[bot].Ammo>0&&actions[bot].SpentMask!=0)
                {
                    if ((WeaponRules.SupportsAds(actions[bot].Weapon)?!actions[bot].Aiming:aiTarget[bot]<0) || !RouletteRules.Has(Game.AliveMask,aiTarget[bot]))
                    {
                        aiTarget[bot] = RandomTarget(bot);
                        if (aiTarget[bot] < 0) continue;
                        AimAiWeapon(bot,aiTarget[bot],now,out float y,out float p);
                        changed |= Look(bot,y,p,WeaponRules.SupportsAds(actions[bot].Weapon),now);
                        double aimDelay=Difficulty==AiDifficulty.Hard?.3:Difficulty==AiDifficulty.Normal?.55:.85;
                        if(Game.ShotTimeLimit>0)aimDelay=Math.Min(aimDelay,Math.Max(.02,(actions[bot].StartedAt+Game.ShotTimeLimit-now)*.5));
                        nextAi[bot] = now + aimDelay;
                    }
                    else
                    {
                        AimAiWeapon(bot,aiTarget[bot],now,out float y,out float p);
                        changed |= Fire(bot,y,p,WeaponRules.SupportsAds(actions[bot].Weapon),actions[bot].Version,now,now);
                        nextAi[bot]=now+AiInterval(random.NextDouble());
                    }
                    continue;
                }
                var state = actions[bot]; int digit = state.Digit(state.Progress);
                if (random.NextDouble() < AiErrorChance) digit = digit % 5 + 1;
                changed |= Input(bot, digit, state.Version, now);
                nextAi[bot] = Math.Max(now, actions[bot].BlockedUntil) + AiInterval(random.NextDouble());
                if(actions[bot].Action==DuelAction.Aim&&Game.ShotTimeLimit>0)
                    nextAi[bot]=Math.Min(nextAi[bot],now+Game.ShotTimeLimit*.25);
            }
            for (int seat=0;seat<RouletteRules.MaxPlayers;seat++)
                if (actions[seat].Aiming && !IsBot(seat) && now-lastLook[seat]>.35)
                { actions[seat].Aiming=false; actions[seat].Target=-1; Sequence++; changed=true; }
            return changed;
        }
        private void ScheduleAi(double now)
        {
            if (BotMask==0) return;
            for (int bot = 1; bot < RouletteRules.MaxPlayers; bot++)
                if (IsBot(bot)&&RouletteRules.Has(Game.AliveMask, bot)) nextAi[bot] = now + AiInterval(random.NextDouble());
        }
        private bool quickTaser(int seat)=>QuickHandsActive&&actions[seat].QuickEquipped&&actions[seat].QuickItem==2;
        private bool HasTurnEffects(){foreach(var a in actions)if(a.NoruFaceActive||a.TaserArmed||a.Stunned||a.Ink.Armed)return true;return AnyInkFlight()||HasQuickEffects();}
        private bool CanClockShoot(int seat,double now) => !shotOpportunitySpent && clock.State.Phase==ClockPhase.Shooting && (clock.State.First==seat&&clockShotsRemaining>0 || actions[seat].TaserArmed&&actions[seat].Weapon==DuelWeapon.Taser) &&
            RouletteRules.Count(Game.AliveMask)>=2 && (Game.ShotTimeLimit==0 || now-clock.State.PhaseAt<Game.ShotTimeLimit);
        public bool StopClock(int seat,int iteration,double sample,double now)
        {
            if(Stage!=DuelStage.Racing || Game.Phase!=DuelPhase.Playing || !RouletteRules.Has(Game.AliveMask,seat) || !clock.Stop(seat,iteration,sample,now))return false;
            if(clock.State.Phase==ClockPhase.Ranking) { RecordClockRanking(now);Sequence++; }
            return true;
        }
        public int MemoryProgress(int seat)=>clock.MemoryProgress(seat);
        public bool StopDice(int seat,int iteration,int diceRound,double now)
        {
            if(Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||!RouletteRules.Has(Game.ConnectedMask,seat)||!clock.StopDice(seat,iteration,diceRound,now))return false;
            Sequence++;return true;
        }
        public bool InputMemory(int seat,int iteration,int memoryRound,int index,int direction,double now)
        {
            if(seat<0||seat>=RouletteRules.MaxPlayers||Stage!=DuelStage.Racing||Game.Phase!=DuelPhase.Playing||!RouletteRules.Has(Game.AliveMask,seat))return false;
            var before=clock.State.Memory;
            if(!clock.InputMemory(seat,iteration,memoryRound,index,direction,now))return false;
            if(clock.State.Phase==ClockPhase.Ranking)RecordClockRanking(now);
            if(before.Phase!=clock.State.Memory.Phase||before.Round!=clock.State.Memory.Round)Sequence++;
            return true;
        }
        private void BeginPartySelection(double now)
        {
            ClearTurnEffects();ResetQuickItems();ResetBulletTurn();shotOpportunitySpent=false;clock.Select(Game.AliveMask,now,Game.ManualMiniGame);
            for(int i=0;i<RouletteRules.MaxPlayers;i++)
            {
                if(RouletteRules.Has(Game.AliveMask,i)){actions[i].Weapon=selectedWeapons[i];ResetAction(i,DuelAction.Pickup,now);lastLook[i]=now;aiTarget[i]=-1;}
                nextAi[i]=double.PositiveInfinity;
            }
            Sequence++;
        }
        private void RecordClockRanking(double now)
        {
            var s=clock.State;
            if(s.MiniGame==PartyMiniGame.Countdown)
            {for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(s.ParticipantsMask,i))
            {var e=s.ForSeat(i);if(e.Centiseconds>=0){actions[i].ClockStops++;actions[i].ClockErrorTotal+=e.Error;}}
            if(s.First>=0)actions[s.First].ClockWins++;}
            int eliminated=Game.Eliminate(s.EliminatedMask);
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(eliminated,i))
                LoseLife(i,DuelDeathCause.ClockTimeout,now);
            Game.ContinueParty();FinishPartyIfReady(now);
            Retarget();
            if(Game.Phase==DuelPhase.Finished)SetStage(DuelStage.AfterShot,now);
        }
        private void FinishQuick(double now)
        {
            ClearQuickItems();
            int winner=-1,loser=-1,rank=0;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(Game.AliveMask,i))winner=i;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)
                if(RouletteRules.Has(clock.State.ParticipantsMask,i)&&i!=winner&&clock.State.ForSeat(i).Rank>rank)
                {rank=clock.State.ForSeat(i).Rank;loser=i;}
            clock.FinishQuick(winner,loser,now);
            Game.ContinueParty();FinishPartyIfReady(now);
            Sequence++;
        }
        private void LoseLife(int seat,DuelDeathCause cause,double now,int count=1)
        {
            if(actions[seat].DiedAt>=0)return;
            FinishQuickItem(seat);
            actions[seat].LivesRemaining=Math.Max(0,actions[seat].LivesRemaining-count);
            actions[seat].DiedAt=now;actions[seat].ReviveAt=-1;actions[seat].DeathCause=cause;
            actions[seat].NoruFaceActive=false;
            actions[seat].Version++;actions[seat].Aiming=false;actions[seat].Target=-1;
            actions[seat].Ink.Armed=false;actions[seat].Ink.Version++;actions[seat].Ink.InkedAt=-1;
            if(actions[seat].LivesRemaining==0)PartyAliveMask&=~(1<<seat);
        }
        private bool FinishPartyIfReady(double now)
        {
            if(clock.State.MiniGame==PartyMiniGame.QuickHands&&(clock.State.Phase==ClockPhase.Dice||QuickHandsActive&&clock.HasQuickTies&&RouletteRules.Count(Game.AliveMask)<=1))return false;
            if(RouletteRules.Count(PartyAliveMask)>1)return false;
            if(PartyAliveMask!=0&&(Game.AliveMask&PartyAliveMask)==0)
            {Game.ContinueParty();if(clock.State.Phase!=ClockPhase.Returning)clock.Return(now);return false;}
            Game.FinishParty(PartyAliveMask);if(Stage!=DuelStage.AfterShot)SetStage(DuelStage.AfterShot,now);return true;
        }
        private bool ScheduleRecovery(double now)
        {
            if(clock.State.Phase!=ClockPhase.Returning&&clock.State.Phase!=ClockPhase.QuickResults)return false;
            double duration=clock.State.Phase==ClockPhase.QuickResults?DuelClockContest.QuickResultsDuration:DuelClockContest.ReturnDuration;
            bool changed=false;
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(PartyAliveMask,i)&&actions[i].DiedAt>=0)
            {
                double start=Math.Max(actions[i].DiedAt+1.05,clock.State.PhaseAt+duration-RecoveryDuration);
                if(actions[i].ReviveAt>=0&&actions[i].ReviveAt<=start)continue;
                actions[i].ReviveAt=start;actions[i].AimYaw=i*60;actions[i].AimPitch=0;changed=true;
            }
            if(changed)Sequence++;return changed;
        }
        private void RestoreParty(double now,bool healSurvivors=true)
        {
            int previousAlive=Game.AliveMask;Game.RestoreParty(PartyAliveMask,healSurvivors);
            for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(PartyAliveMask,i))
            {
                if(healSurvivors||!RouletteRules.Has(previousAlive,i)){actions[i].Life++;actions[i].Ink.InkedAt=-1;}
                actions[i].DiedAt=actions[i].HitAt=actions[i].FiredAt=-1;actions[i].ReviveAt=-1;
                actions[i].DeathCause=DuelDeathCause.None;actions[i].FiredTarget=-1;actions[i].CurrentStreak=0;
                actions[i].Weapon=selectedWeapons[i];actions[i].AimStartedAt=0;
                actions[i].LoadedMask=actions[i].SpentMask=actions[i].NextChamber=0;
                ResetAction(i,DuelAction.Pickup,now);lastLook[i]=now-1;aiTarget[i]=-1;
            }
            Sequence++;
        }
        private void ScheduleMiniAi(int seat)
        {
            var s=clock.State;double r=random.NextDouble();
            if(s.MiniGame==PartyMiniGame.Memory)
                nextAi[seat]=s.Memory.Phase==ArrowMemoryPhase.Input?s.Memory.PhaseAt+AiInterval(r):double.PositiveInfinity;
            else if(s.MiniGame==PartyMiniGame.Signal)
            {
                double delay=Difficulty==AiDifficulty.Easy?.5+r*.6:Difficulty==AiDifficulty.Normal?.25+r*.35:.12+r*.18;
                nextAi[seat]=random.NextDouble()<AiErrorChance?s.StartedAt+1:clock.ReactionGoAt+delay;
            }
            else if(s.MiniGame==PartyMiniGame.Greed)
                nextAi[seat]=s.StartedAt+(Difficulty==AiDifficulty.Easy?8+r*15:Difficulty==AiDifficulty.Normal?12+r*13:16+r*11);
            else
            {double error=Difficulty==AiDifficulty.Easy?1.8:Difficulty==AiDifficulty.Normal?.75:.25;nextAi[seat]=s.StartedAt+s.Target/100.0+(r*2-1)*error;}
        }
        private bool TickClock(double now,bool manualChosen=false)
        {
            bool changed=false;
            if(clock.State.Phase==ClockPhase.Dice)
                for(int bot=1;bot<RouletteRules.MaxPlayers;bot++)
                    if(IsBot(bot)&&clock.State.Dice.CanStop(bot)&&now>=clock.State.Dice.StartedAt+.8+(bot*37+clock.State.Dice.Round*17)%43*.1)
                        changed|=StopDice(bot,clock.State.Iteration,clock.State.Dice.Round,now);
            for(int bot=1;bot<RouletteRules.MaxPlayers;bot++)
                if(IsBot(bot)&&clock.State.Phase==ClockPhase.Memory&&clock.State.Memory.Phase==ArrowMemoryPhase.Input&&RouletteRules.Has(clock.State.Memory.ActiveMask&Game.AliveMask,bot)&&now>=nextAi[bot]&&!clock.State.Memory.ForSeat(bot).Locked)
                {
                    var m=clock.State.Memory;int index=clock.MemoryProgress(bot),direction=clock.MemoryDirection(index);
                    double chance=Math.Min(.45,AiErrorChance+Math.Max(0,m.Length-3)*(Difficulty==AiDifficulty.Easy?.008:Difficulty==AiDifficulty.Normal?.004:.002));
                    if(random.NextDouble()<chance)direction=(direction+1+random.Next(3))%4;
                    changed|=InputMemory(bot,clock.State.Iteration,m.Round,index,direction,now);
                    nextAi[bot]=now+AiInterval(random.NextDouble());
                }
                else if(IsBot(bot)&&clock.State.Phase==ClockPhase.Counting&&RouletteRules.Has(Game.AliveMask,bot)&&now>=nextAi[bot])
                { double sample=nextAi[bot];nextAi[bot]=double.PositiveInfinity;changed |= StopClock(bot,clock.State.Iteration,sample,sample); }
            var before=clock.State.Phase;
            var memoryBefore=clock.State.Memory.Phase;
            if(clock.Tick(now,Game.AliveMask,Game.ShotTimeLimit,AnyProjectile(),Game.ManualMiniGame,manualChosen,Game.ConnectedMask))
            {
                changed=true;Sequence++;
                if(before==ClockPhase.Selecting && (clock.State.Phase==ClockPhase.Counting||clock.State.Phase==ClockPhase.Memory))
                    for(int i=0;i<RouletteRules.MaxPlayers;i++)
                    {if(RouletteRules.Has(Game.AliveMask,i)){actions[i].Weapon=DuelWeapon.Revolver;ResetAction(i,DuelAction.Pickup,now);}ScheduleMiniAi(i);}
                if(clock.State.Phase==ClockPhase.Memory&&clock.State.Memory.Phase==ArrowMemoryPhase.Input&&memoryBefore!=ArrowMemoryPhase.Input)
                    for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(clock.State.Memory.ActiveMask,i))ScheduleMiniAi(i);
                if(before==ClockPhase.Selecting && clock.State.Phase==ClockPhase.ItemPreparation && clock.State.MiniGame==PartyMiniGame.QuickHands)
                    for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(Game.AliveMask,i)){actions[i].Weapon=selectedWeapons[i];actions[i].SpentMask=0;actions[i].NextChamber=0;ResetAction(i,DuelAction.Pickup,now);}
                if(before==ClockPhase.QuickCountdown && clock.State.Phase==ClockPhase.QuickHands)
                {for(int i=0;i<RouletteRules.MaxPlayers;i++)if(RouletteRules.Has(Game.AliveMask,i)){actions[i].Weapon=selectedWeapons[i];ResetAction(i,DuelAction.Pickup,now);lastLook[i]=now-1;aiTarget[i]=-1;}ScheduleAi(now);}
                if((before==ClockPhase.Counting||before==ClockPhase.Memory||before==ClockPhase.Dice)&&clock.State.Phase==ClockPhase.Ranking)RecordClockRanking(now);
                if(before==ClockPhase.Dice&&clock.State.Phase==ClockPhase.QuickResults){Game.ContinueParty();FinishPartyIfReady(now);}
                if(before!=ClockPhase.Shooting && clock.State.Phase==ClockPhase.Shooting)
                {
                    shotOpportunitySpent=false;
                    clockShotsRemaining=1+(actions[clock.State.First].BulletUsed?1:0);
                    for(int i=0;i<actions.Length;i++)if(actions[i].TaserArmed&&RouletteRules.Has(Game.AliveMask,i))
                    {actions[i].Weapon=DuelWeapon.Taser;actions[i].Action=DuelAction.Aim;actions[i].LoadedMask=1;actions[i].SpentMask=0;actions[i].NextChamber=0;actions[i].Version++;actions[i].ShootingStartedAt=now;actions[i].Aiming=false;nextAi[i]=now+.4;}
                    int seat=clock.State.First;var a=actions[seat];a.Action=DuelAction.Aim;a.Progress=0;a.Version++;a.StartedAt=now;a.ShootingStartedAt=now;a.Aiming=false;a.LoadedMask=1;a.SpentMask=0;a.NextChamber=0;
                    int target=RouletteRules.Has(Game.AliveMask,clock.State.Last)?clock.State.Last:RandomTarget(seat);
                    DuelGunplay.Angles(DuelGunplay.Point(target,DuelHitPart.Body)-DuelGunplay.Eye(seat),out a.AimYaw,out a.AimPitch);
                    actions[seat]=a;nextAi[seat]=now+(Difficulty==AiDifficulty.Hard?.3:Difficulty==AiDifficulty.Normal?.55:.85);
                }
                else if(clock.State.Phase==ClockPhase.Returning)
                {
                    int seat=clock.State.First;if(seat>=0){actions[seat].Action=DuelAction.Pickup;actions[seat].Aiming=false;actions[seat].Target=-1;actions[seat].Version++;}
                }
            }
            var state=clock.State;
            changed|=TickAiItemPreparation(now);
            if(state.Phase==ClockPhase.Shooting)
                for(int bot=1;bot<actions.Length;bot++)if(IsBot(bot)&&actions[bot].Weapon==DuelWeapon.Taser&&actions[bot].Ammo>0&&!actions[bot].Stunned&&!DuelInk.Busy(actions[bot].Ink,now)&&now>=nextAi[bot])
                {int target=RandomTarget(bot);if(target<0)continue;AimAiWeapon(bot,target,now,out float y,out float x);bool fired=Fire(bot,y,x,false,actions[bot].Version,now,now);changed|=fired;nextAi[bot]=fired?double.PositiveInfinity:now+AiInterval(.5);}
            if(state.Phase==ClockPhase.Shooting&&IsBot(state.First)&&!actions[state.First].Stunned&&!DuelInk.Busy(actions[state.First].Ink,now)&&now>=nextAi[state.First])
            {
                int bot=state.First;
                int target=RandomTarget(bot);
                if(target>=0){AimAiWeapon(bot,target,now,out float yaw,out float pitch);bool fired=Fire(bot,yaw,pitch,WeaponRules.SupportsAds(actions[bot].Weapon),actions[bot].Version,now,now);changed|=fired;nextAi[bot]=fired?double.PositiveInfinity:now+AiInterval(.5);}
            }
            if(state.Phase==ClockPhase.Returning||state.Phase==ClockPhase.QuickResults||Game.Phase!=DuelPhase.Playing)ClearTurnEffects();
            changed|=ScheduleRecovery(now);
            if(clock.State.Phase==ClockPhase.Returning&&now-clock.State.PhaseAt>=DuelClockContest.ReturnDuration)
            {RestoreParty(now,false);if(!FinishPartyIfReady(now))BeginPartySelection(now);changed=true;}
            if(clock.State.Phase==ClockPhase.QuickResults&&now-clock.State.PhaseAt>=DuelClockContest.QuickResultsDuration&&Game.Phase==DuelPhase.Playing)
            {RestoreParty(now);if(!FinishPartyIfReady(now))BeginPartySelection(now);changed=true;}
            return changed;
        }
        private int RandomTarget(int actor)
        {
            int count = 0;
            for (int i = 0; i < RouletteRules.MaxPlayers; i++) if (i != actor && RouletteRules.Has(Game.AliveMask, i)) count++;
            if (count == 0) return -1;
            int selected = random.Next(count);
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
                if (i != actor && RouletteRules.Has(Game.AliveMask, i) && selected-- == 0) return i;
            return -1;
        }
        private void Retarget()
        {
            for (int i = 0; i < RouletteRules.MaxPlayers; i++)
                if (actions[i].Target >= 0 && !RouletteRules.Has(Game.AliveMask,actions[i].Target)) actions[i].Target = -1;
        }
        public bool Disconnect(int seat, double now)
        {
            if(!RouletteRules.Has(Game.ConnectedMask,seat))return false;
            FinishQuickItem(seat);
            bool playing=Game.Phase==DuelPhase.Playing;
            PartyAliveMask&=~(1<<seat);
            if (!Game.Leave(seat)) return false;
            BotMask&=~(1<<seat);
            InvalidatePracticeFlights();
            actions[seat].LivesRemaining=0;actions[seat].ReviveAt=-1;projectiles[seat]=null;
            inkFlights[seat]=null;actions[seat].Ink.Armed=false;actions[seat].Ink.Version++;
            practiceProjectiles.RemoveAll(v=>v.Actor==seat);
            actions[seat].Version++; Retarget(); Sequence++;
            if(playing)
            {
                Game.ContinueParty();
                if(!FinishPartyIfReady(now)&&QuickHandsActive&&RouletteRules.Count(Game.AliveMask)<=1)FinishQuick(now);
            }
            else TryStart(now);
            return true;
        }
    }
}


