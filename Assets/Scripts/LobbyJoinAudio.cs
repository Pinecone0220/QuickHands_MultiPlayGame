using UnityEngine;

namespace HelloWorld
{
    // Observe the owner's authoritative snapshot on both host and remote clients.
    public sealed class LobbyJoinAudio : MonoBehaviour
    {
        private HelloWorldPlayer session;
        private AudioSource source;
        private AudioClip joinClip,leaveClip;
        private DuelSnapshot lastState;
        private void Awake()
        {
            source=gameObject.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.loop=false;
            source.spatialBlend=0;
            source.volume=.55f;
            joinClip=Resources.Load<AudioClip>("UIAudio/LobbyJoin");
            leaveClip=Resources.Load<AudioClip>("UIAudio/LobbyLeave");
        }
        private void Update()
        {
            var local=HelloWorldPlayer.Local;
            if(local!=null&&!local.IsSpawned)local=null;
            if(ReferenceEquals(session,local))return;
            bool leavingLobby=!ReferenceEquals(session,null)&&IsLobby(lastState)&&lastState.ConnectedMask!=0;
            Detach();
            if(leavingLobby)Play(leaveClip);
            session=local;
            if(session==null)return;
            session.State.OnValueChanged+=Changed;
            // One welcome sound, rather than replaying every existing occupant's arrival.
            Changed(default,session.State.Value);
        }
        private void Changed(DuelSnapshot before,DuelSnapshot after)
        {
            lastState=after;
            if(IsLobby(after)&&(after.ConnectedMask&~before.ConnectedMask)!=0)Play(joinClip);
            else if(IsLobby(before)&&(before.ConnectedMask&~after.ConnectedMask)!=0)Play(leaveClip);
        }
        private static bool IsLobby(DuelSnapshot state)=>state.Mode==DuelMode.PvP&&state.Stage==DuelStage.Idle&&state.Phase!=DuelPhase.Playing;
        private void Play(AudioClip clip)
        {
            if(clip==null)return;
            source.clip=clip;source.volume=.55f*DuelSettings.Effects;
            source.Play();
        }
        private void Detach()
        {
            if(!ReferenceEquals(session,null))session.State.OnValueChanged-=Changed;
            session=null;
            lastState=default;
        }
        private void OnDestroy(){Detach();if(source!=null)source.Stop();}
    }
}
