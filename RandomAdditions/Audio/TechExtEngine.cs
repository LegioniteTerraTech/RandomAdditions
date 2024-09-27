using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FMOD;

namespace RandomAdditions
{
    public class TechExtEngine
    {
        public FactionSubTypes faction;
        internal CorpExtAudio CEA;
        internal TechExtAudioState idle;
        internal TechExtAudioState start;
        internal TechExtAudioStateWithLoopedFrequency run;
        internal TechExtAudioState stop;
        private float EngineSetVolume = 1;
        private bool lastEngineState = false;
        private bool lastDriveState = false;

        public TechExtEngine(int corp, CorpExtAudio corpAudio)
        {
            faction = (FactionSubTypes)corp;
            CEA = corpAudio;
            idle = new TechExtAudioState(CEA.CorpEngineAudioIdle, true);
            start = new TechExtAudioState(CEA.CorpEngineAudioStart, false);
            run = new TechExtAudioStateWithLoopedFrequency(CEA.CorpEngineAudioRunning);
            stop = new TechExtAudioState(CEA.CorpEngineAudioStop, false);
        }

        internal void EngineStop()
        {
            stop.Stop();
            idle.Stop();
            start.Stop();
            run.Stop();
        }
        internal void EngineUpdate(bool FwdRevMove, bool moving)
        {
            if (FwdRevMove != lastEngineState)
            {
                if (FwdRevMove)
                    start.Play(EngineSetVolume, true);
                else
                    stop.Play(EngineSetVolume, true);
                //UnityEngine.Debug.Log("FwdRevMove - " + FwdRevMove);
                lastEngineState = FwdRevMove;
            }
            if (moving != lastDriveState)
            {
                if (moving)
                {
                    run.PlayLooped(EngineSetVolume, false);
                    idle.Stop();
                }
                else
                {
                    idle.PlayLooped(EngineSetVolume, false);
                    run.Stop();
                    run.ResetFrequency();
                }
                //UnityEngine.Debug.Log("moving - " + FwdRevMove);
                lastDriveState = moving;
            }
        }
        internal void EnginePosUpdate(Vector3 pos)
        {
            idle.UpdateThis(pos);
            run.UpdateThis(pos);
            start.UpdateThis(pos);
            stop.UpdateThis(pos);
        }
        internal void EnginePitchUpdate(float Throttle, float Speed)
        {
            run.UpdateFrequency(Mathf.Clamp(Throttle + (Speed / CEA.EngineIdealSpeed), 0, CEA.EnginePitchMax));
            //run.UpdateFrequency(Mathf.Clamp(Throttle / EnginePitchDeepener, 0, EngineMaxPitch));
        }
        internal void EngineVolumeUpdate(float vol, TechAudio.UpdateAudioCache cache)
        {
            EngineSetVolume = vol;
            if (cache.corpPercentages.TryGetValue((int)faction, out float percent))
            {
                EngineSetVolume *= percent * CEA.EngineVolumeMulti;
                //UnityEngine.Debug.Log("EngineSetVolume - Faction " + (int)faction + " percent " + percent.ToString("F") + ", volume " + EngineSetVolume.ToString("F"));
            }
            else
            {
                //UnityEngine.Debug.Log("EngineSetVolume - no corpPercentages");
                EngineSetVolume = 0;
            }
            idle.UpdateVolume(EngineSetVolume);
            run.UpdateVolume(EngineSetVolume);
            start.UpdateVolume(EngineSetVolume);
            stop.UpdateVolume(EngineSetVolume);
        }
    }
}