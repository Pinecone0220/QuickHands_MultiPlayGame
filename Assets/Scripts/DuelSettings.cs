using UnityEngine;
namespace HelloWorld
{
    public static class DuelSettings
    {
        const string MusicKey="qh.settings.music",EffectsKey="qh.settings.effects",SensitivityKey="qh.settings.sensitivity";
        static bool loaded;static float music,effects,sensitivity;
        public static bool WindowOpen {get;set;}
        static void Load(){if(loaded)return;loaded=true;music=Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey,1));effects=Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey,1));sensitivity=Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey,1),.25f,2.5f);}
        public static float Music {get{Load();return music;}set{Load();music=Mathf.Clamp01(value);PlayerPrefs.SetFloat(MusicKey,music);PlayerPrefs.Save();}}
        public static float Effects {get{Load();return effects;}set{Load();effects=Mathf.Clamp01(value);PlayerPrefs.SetFloat(EffectsKey,effects);PlayerPrefs.Save();}}
        public static float Sensitivity {get{Load();return sensitivity;}set{Load();sensitivity=Mathf.Clamp(value,.25f,2.5f);PlayerPrefs.SetFloat(SensitivityKey,sensitivity);PlayerPrefs.Save();}}
    }
}
