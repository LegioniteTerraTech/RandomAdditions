using System;
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
        public static readonly List<TimeTank> tanks = new List<TimeTank>();

        public static bool TimeControllerPresent = false;// Is the time locked to a master block?

        public class ClockManager : MonoBehaviour
        {
            private static ClockManager inst;
            //The global timekeeper for all clocks
            //  think of it as the "atomic clock" of the offworld.
            //  Also handles setting the time and then locking it.
            public static void Initiate()
            {
                if (inst)
                    return;
                inst = new GameObject("GlobalClockGeneral").AddComponent<ClockManager>();
                clocks.Clear();
                tanks.Clear();
                DebugRandAddi.Log("RandomAdditions: Created GlobalClock.");
            }
            public static void DeInit()
            {
                if (!inst)
                    return;
                Destroy(inst.gameObject);
                inst = null;
                DebugRandAddi.Log("RandomAdditions: DeInit GlobalClock.");
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
                    //Debug.Log("RandomAdditions: Time Controller present.");
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
                foreach (TimeTank tonk in tanks)
                {
                    tonk.ResetUIValid();
                }
            }

            private void Update()
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
            }
        }

        public class TimeTank : MonoBehaviour
        {
            //This handles the GUI clock used on the Tank.  Know your time and set your mines
            //  Charge your tech with solars before nightfall.
            private Tank tank;
            //private ClockManager man;
            public bool DisplayTimeTank = false;
            public void Initiate()
            {
                tank = gameObject.GetComponent<Tank>();
                tanks.Add(this);
            }

            internal void ResetUIValid()
            {
                DisplayTimeTank = false;
            }
        }
    }
}
