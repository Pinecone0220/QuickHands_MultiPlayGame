using UnityEngine;

namespace HelloWorld
{
    public sealed class DuelBackgroundMusic : MonoBehaviour
    {
        private AudioSource music;
        public bool GachaOpen { get; set; }
        private void Awake()
        {
            music=gameObject.AddComponent<AudioSource>();
            music.playOnAwake=false;music.loop=true;music.spatialBlend=0;
            music.priority=220;music.volume=0;
            music.clip=Resources.Load<AudioClip>("Music/LocalForecastElevator");
            if(music.clip!=null)music.Play();
        }
        private void Update()
        {
            var player=HelloWorldPlayer.Local;
            bool playing=player!=null&&player.IsSpawned&&player.State.Value.Phase==DuelPhase.Playing;
            float target=(GachaOpen?.14f:playing?.16f:.28f)*DuelSettings.Music;
            music.volume=Mathf.MoveTowards(music.volume,target,Time.unscaledDeltaTime*.13f);
        }
        private void OnDestroy(){if(music!=null)music.Stop();}
    }
}
