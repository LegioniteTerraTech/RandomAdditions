using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace RandomAdditions
{
    public class GlobalClock // The clock that handles all clocks
    {
        //Global Variables
        public static bool SetByGUI = false;
        public static bool LockTime = false;
        public static int SavedTime = 0;// The Hour to keep the world set to 
        internal static int LastHour = 0;     // Last Hour 
        public static readonly List<ModuleClock> clocks = new List<ModuleClock>();
        public static readonly List<RandomTank> tanks = new List<RandomTank>();

        public static bool TimeControllerPresent = false;// Is the time locked to a master block?
        public static EventNoParams SlowUpdateEvent = new EventNoParams();

        public class ClockManager : MonoBehaviour
        {
            private static ClockManager inst;
            //The global timekeeper for all clocks
            //  think of it as the "atomic clock" of the offworld.
            //  Also handles setting the time and then locking it.
            //   Also handles static updating of various other modules.
            public static void Initiate()
            {
                if (inst)
                    return;
                inst = new GameObject("GlobalClockGeneral").AddComponent<ClockManager>();
                clocks.Clear();
                tanks.Clear();
                ManTechs.inst.PlayerTankChangedEvent.Subscribe(inst.PlayerTechUpdate);
                DebugRandAddi.Log("RandomAdditions: Created GlobalClock.");
                inst.gameObject.SetActive(true);
                inst.enabled = true;
            }
            public static void DeInit()
            {
                if (!inst)
                    return;
                ManTechs.inst.PlayerTankChangedEvent.Unsubscribe(inst.PlayerTechUpdate);
                Destroy(inst.gameObject);
                inst = null;
                DebugRandAddi.Log("RandomAdditions: DeInit GlobalClock.");
            }


            public void PlayerTechUpdate(Tank Tech, bool set)
            {
                if (Tech)
                {
                    foreach (var item in ManTechs.inst.IterateTechs())
                    {
                        if (item)
                            RandomTank.Ensure(item).ReevaluateLoadingDiameter();
                    }
                }
            }

            //All ModuleClock(s) will control the time based on global values.
            //  The latest Techs will override this however.

            public void GetTimeSetClocks()
            {
                LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                UpdateTanks();
                TimeControllerPresent = UpdateClocks();
                if (TimeControllerPresent && LockTime)
                {
                    //DebugRandAddi.Log("RandomAdditions: Time Controller present.");
                    if (SetByGUI)
                    {
                        SetAllTimeControllers();
                        LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                        UpdateClocks();
                    }
                    else
                    {
                        GetFirstTimeControllerAndSetTime();
                        LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                        UpdateClocks();
                    }
                }
                else
                    SavedTime = LastHour;
                SetByGUI = false;
                GUIClock.allowTimeControl = TimeControllerPresent && (!ManNetwork.IsNetworked || ManNetwork.IsHost);
                GUIClock.GetTime();
            }

            public void GetFirstTimeControllerAndSetTime()
            {
                foreach (ModuleClock clunk in clocks)
                {
                    if (clunk.ControlTime && clunk.IsAttached)
                    {
                        SavedTime = clunk.SavedTime;
                        SetTime();
                        break;
                    }
                }
            }
            public void SetAllTimeControllers()
            {
                foreach (ModuleClock clunk in clocks)
                {
                    clunk.SavedTime = SavedTime;
                }
                SetTime();
            }
            public void SetTime()
            {
                DebugRandAddi.Info("RandomAdditions: Time Change external queued!");
                if (ManTimeOfDay.inst.TimeOfDay != SavedTime)
                    Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(SavedTime, 0, 0, false);
                DebugRandAddi.Info("RandomAdditions: Time Changed to " + Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay);
            }

            public bool UpdateClocks()
            {
                bool timeControl = false;
                foreach (ModuleClock clunk in clocks)
                {
                    if (clunk.IsAttached)
                    {
                        if (!timeControl)
                            timeControl = clunk.SetClock();
                        else
                            clunk.SetClock();
                    }
                }
                return timeControl;
            }
            public void UpdateTanks()
            {
                foreach (RandomTank tonk in tanks)
                {
                    tonk.ResetUIValid();
                }
            }

            private const float SlowUpdateTime = 0.6f;
            private float SlowUpdate = 0;
            public void Update()
            {
                if (ManTimeOfDay.inst.TimeOfDay != SavedTime)
                {
                    DebugRandAddi.Info("RandomAdditions: Time Changed!");
                    GetTimeSetClocks();
                }
                else if (SetByGUI)
                {
                    DebugRandAddi.Log("RandomAdditions: Time Changed by Player!");
                    GetTimeSetClocks();
                }
                ExtUsageHint.UpdateHintTimers();

                if (SlowUpdate < Time.time)
                {
                    SlowUpdate = Time.time + SlowUpdateTime;
                    UpdateSlow();
                }
            }
            private void UpdateSlow()
            {
                SlowUpdateEvent.Send();
                ProjBase.UpdateSlow();
            }
        }

    }
}
