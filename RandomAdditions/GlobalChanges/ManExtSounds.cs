using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace RandomAdditions
{
    public class ModuleSoundOverride : ExtModule
    {
        internal FMODEventInstance sound;
    }

    public class ManExtSounds
    {
    }
    public static class OverrideAudio
    {
        private static readonly FieldInfo
            SFX = typeof(AudioProvider).GetField("m_SFXType", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX1 = typeof(AudioProvider).GetField("m_AttackTime", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX2 = typeof(AudioProvider).GetField("m_ReleaseTime", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX3 = typeof(AudioProvider).GetField("m_Adsr01", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX4 = typeof(AudioProvider).GetField("m_State", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX5 = typeof(AudioProvider).GetField("m_NoteOn", BindingFlags.NonPublic | BindingFlags.Instance),
            SFX6 = typeof(AudioProvider).GetField("m_RequestedByModule", BindingFlags.NonPublic | BindingFlags.Instance),
            addSFX = typeof(ModuleAudioProvider).GetField("m_LoopedAdsrSFX", BindingFlags.NonPublic | BindingFlags.Instance),
            addSFX2 = typeof(ModuleAudioProvider).GetField("m_SFXLookup", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<int, TechAudio.TechAudioEventSimple> allSounds = new Dictionary<int, TechAudio.TechAudioEventSimple>();



        /// <summary>
        /// Shoehorns in audio for a module that would not be able to otherwise
        /// </summary>
        /// <param name="Audio"></param>
        /// <param name="type"></param>
        public static void AddToSounds(ref ModuleAudioProvider Audio, TechAudio.SFXType type)
        {
            if (Audio != null)
            {
                List<AudioProvider> APA = (List<AudioProvider>)addSFX.GetValue(Audio);
                Dictionary<TechAudio.SFXType, AudioProvider> APA2 = (Dictionary<TechAudio.SFXType, AudioProvider>)addSFX2.GetValue(Audio);
                if (APA != null)
                {
                    if (!APA.Exists(delegate (AudioProvider cand) { return cand.SFXType == type; }))
                    {
                        AudioProvider AP = ForceMakeNew(type, Audio);
                        APA.Add(AP);
                        APA2.Add(type, AP);
                    }
                }
                else
                {
                    AudioProvider AP = ForceMakeNew(type, Audio);
                    APA = new List<AudioProvider> { AP };
                    APA2.Add(type, AP);
                }
                addSFX.SetValue(Audio, APA);
                addSFX2.SetValue(Audio, APA2);
            }
        }
        private static AudioProvider ForceMakeNew(TechAudio.SFXType type, Module executing)
        {
            AudioProvider aud = new AudioProvider();
            SFX.SetValue(aud, type);
            SFX1.SetValue(aud, 1f);
            SFX2.SetValue(aud, 1f);
            SFX3.SetValue(aud, 1f);
            SFX4.SetValue(aud, 0);
            SFX5.SetValue(aud, false);
            SFX6.SetValue(aud, null);
            aud.SetParent(executing);
            return aud;
        }
    }
}
