using System;
using System.Collections;
using System.Collections.Generic;
using TerraTechETCUtil;
using UnityEngine;

public class SFXPlayNow : RandomAdditions.SFXPlayNow { }

namespace RandomAdditions
{
    /// <summary>
    /// Plays the SFX immedeately one-shot
    /// </summary>
    public class SFXPlayNow : MonoBehaviour
    {
        private class SoundTracker
        {
            public AudioInst inst;
            public float nextCanPlayTime;
        }
        private static Dictionary<string, Dictionary<string, SoundTracker[]>> instsByGOName = 
            new Dictionary<string, Dictionary<string, SoundTracker[]>>();

        public string Name = "unknown";
        public float Volume = 1;
        public float PitchVariance = 0.025f;
        public float Cooldown = 1;
        public int MaxInstances = 1;

        private SoundTracker[] ourTracked;

        private void OnSpawn()
        {
            enabled = true;
        }

        /// <summary>
        /// Only should fire once on spawn!
        /// </summary>
        public void Update()
        {
            string goName = transform.name.NullOrEmpty() ? "<NULL>" : transform.name;
            DebugRandAddi.Info("GameObject " + goName + " has SFXPlayNow ACTIVE");

            if (ourTracked == null)
            {
                if (!instsByGOName.TryGetValue(goName, out var inst))
                {
                    inst = new Dictionary<string, SoundTracker[]>();
                    instsByGOName.Add(goName, inst);
                }
                if (!inst.TryGetValue(Name, out var tracked))
                {
                    foreach (var wavNameWithExtNSound in ManAudioExt.AllSounds)
                    {
                        if (wavNameWithExtNSound.Value.TryGetValue(Name, out ManAudioExt.AudioGroup instGroup))
                        {
                            DebugRandAddi.Info("Assigned sound (guess) " + Name);
                            tracked = new SoundTracker[MaxInstances];
                            for (int i = 0; i < MaxInstances; i++)
                            {
                                tracked[i] = new SoundTracker()
                                {
                                    inst = instGroup.main[0].Copy(),
                                    nextCanPlayTime = -10,
                                };
                            }
                            break;
                        }
                    }
                    inst.Add(Name, tracked);
                }
                ourTracked = tracked;
            }
            if (ourTracked != null)
            {
                for (int i = 0; i < ourTracked.Length; i++)
                {
                    var sound = ourTracked[i];
                    if (sound.nextCanPlayTime < Time.time)
                    {
                        sound.nextCanPlayTime = Time.time + Cooldown;
                        sound.inst.Play(false, transform.position);
                    }
                }
            }
            enabled = false;
        }
    }
}
