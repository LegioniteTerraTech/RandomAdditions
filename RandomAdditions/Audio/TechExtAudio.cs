using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FMOD;

namespace RandomAdditions
{
    /// <summary>
    /// Here, we host additional SFX for the time being
    /// </summary>
    public class TechExtAudio : MonoBehaviour
    {
        public static List<TechExtAudio> techA = new List<TechExtAudio>();

        private Tank tank;
        internal Dictionary<int, TechExtEngine> engines = new Dictionary<int, TechExtEngine>();
        private TechAudio.UpdateAudioCache Cache;
        private static FieldInfo drive = typeof(TechAudio).GetField("m_Drive", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo turn = typeof(TechAudio).GetField("m_Turn", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo cache = typeof(TechAudio).GetField("m_Cache", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo throttle = typeof(TechAudio).GetField("m_Throttle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo damp = typeof(TechAudio).GetField("m_DampedRoadForce", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo road = typeof(TechAudio).GetField("m_RoadForceCompensate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo scale = typeof(TechAudio).GetField("m_RoadForceScale", BindingFlags.NonPublic | BindingFlags.Instance);


        internal static void debugEncapsulated(string error)
        {
            UnityEngine.Debug.Log(error);
        }
        public static TechExtAudio Insure(Tank tank)
        {
            var rt = tank.GetComponent<TechExtAudio>();
            if (!rt)
            {
                rt = tank.gameObject.AddComponent<TechExtAudio>();
                rt.Initiate();
            }
            return rt;
        }
        public void Initiate()
        {
            tank = gameObject.GetComponent<Tank>();
            //debugEncapsulated("Added TechExtAudio to Tech - " + tank.name);
            Cache = (TechAudio.UpdateAudioCache)cache.GetValue(tank.TechAudio);
            if (Cache == null)
                throw new NullReferenceException("Initiate() - cache is null");
            tank.TankRecycledEvent.Subscribe(Recycle);
            tank.UpdateEvent.Subscribe(OnUpdate);
            techA.Add(this);
            enabled = true;
        }
        public static void ResetAll()
        {
            foreach (var item in techA)
                item.Reset();
        }
        public static void CalibrateAll()
        {
            foreach (var item in techA)
                item.EnginePrecheck();
        }
        private void Reset()
        {
            foreach (var item in engines)
            {
                //Cache.corpPercentages.Remove(item.Key);
                item.Value.EngineStop();
            }
            engines.Clear();
        }
        private void Recycle(Tank tech)
        {
            if (tech == tank)
            {
                var TEA = tech.GetComponent<TechExtAudio>();
                if (TEA)
                {
                    //debugEncapsulated("Recycle TEA - " + tank.name);
                    techA.Remove(TEA);
                    foreach (var item in engines)
                    {
                        Cache.corpPercentages.Remove(item.Key);
                        item.Value.EngineStop();
                    }
                    tank.UpdateEvent.Unsubscribe(OnUpdate);
                    tank.TankRecycledEvent.Unsubscribe(Recycle);
                    Destroy(TEA);
                }
            }
        }

        private void OnUpdate()
        {
            EnginePrecheck();
            VolumeUpdate(ManMusicEnginesExt.currentSFXVol);
            float Speed = 0;
            if (tank.rbody)
                Speed = tank.rbody.velocity.magnitude;
            float Drive = (float)drive.GetValue(tank.TechAudio);
            float Throttle = (float)throttle.GetValue(tank.TechAudio);
            //if (Throttle != 0) debugEncapsulated("engine throttle - " + Throttle);
            try
            {
                foreach (var item in engines)
                {
                    item.Value.EnginePitchUpdate(Throttle, Speed);
                }
            }
            catch (Exception e)
            {
                debugEncapsulated("engine pitch - " + e);
            }
            try
            {
                Vector3 pos = tank.trans.position;
                foreach (var item in engines)
                {
                    item.Value.EnginePosUpdate(pos);
                }
            }
            catch (Exception e)
            {
                debugEncapsulated("engine position - " + e);
            }
            try
            {
                bool FwdRevMove = Cache.wheelsGroundedNum > 0 && Drive != 0;
                bool moving = Cache.wheelsGroundedNum > 0 && Throttle != 0;
                foreach (var item in engines)
                {
                    item.Value.EngineUpdate(FwdRevMove, moving);
                }
            }
            catch (Exception e)
            {
                debugEncapsulated("engine active update - " + e);
            }
        }

        public void EnginePrecheck()
        {
            tank.UpdateEvent.Unsubscribe(EnginePrecheck);
            InsureEngines();
            VolumeUpdate(ManMusicEnginesExt.currentSFXVol);
        }


        internal void InsureEngines()
        {
            foreach (var item in ManMusicEnginesExt.corps)
            {
                int corp = item.Key;
                CorpExtAudio CEA = item.Value;
                if (CEA.hasEngineAudio)
                {
                    if (!engines.ContainsKey(corp))
                    {
                        //debugEncapsulated("New corp engine for " + CEA.ID);
                        engines.Add(corp, new TechExtEngine(corp, CEA));
                        /*
                        if (!Cache.corpPercentages.ContainsKey(corp))
                            Cache.corpPercentages.Add(corp, 0);
                        */
                    }
                }
                //else debugEncapsulated("Corp " + CEA.ID + " has no audio");
            }
        }
        public void VolumeUpdate(float volume)
        {
            //debugEncapsulated("VolumeUpdate " + volume);
            if (Cache == null)
                throw new NullReferenceException("VolumeUpdate - cache is null");
            foreach (var item in engines)
            {
                item.Value.EngineVolumeUpdate(volume, Cache);
            }
        }
    }
}