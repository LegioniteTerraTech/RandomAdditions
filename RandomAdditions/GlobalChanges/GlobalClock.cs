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
        public static List<ModuleClock> clocks;
        public static List<TimeTank> tanks;

        public static bool TimeControllerPresent = false;// Is the time locked to a master block?

        public class ClockManager : MonoBehaviour
        {
            //The global timekeeper for all clocks
            //  think of it as the "atomic clock" of the offworld.
            //  Also handles setting the time and then locking it.
            public static void Initiate()
            {
                new GameObject("GlobalClockGeneral").AddComponent<ClockManager>();
                clocks = new List<ModuleClock>();
                tanks = new List<TimeTank>();
                Debug.Log("RandomAdditions: Created GlobalClock.");
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
                GUIClock.allowTimeControl = TimeControllerPresent;
                GUIClock.GetTime();
            }

            public void GetFirstTimeControllerAndSetTime()
            {
                foreach (ModuleClock clunk in clocks)
                {
                    if (clunk.ControlTime)
                    {
                        SavedTime = clunk.SavedTime;
                        break;
                    }
                }
                SetTime();
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
                //var thisInst = gameObject.GetComponent<ModuleClock>();
                if (ManTimeOfDay.inst.TimeOfDay != SavedTime)
                    Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(SavedTime, 0, 0, false);
            }

            public bool UpdateClocks()
            {
                bool timeControl = false;
                foreach (ModuleClock clunk in clocks)
                {
                    if (!timeControl)
                        timeControl = clunk.SetClock();
                    else
                        clunk.SetClock();
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
                    Debug.Log("RandomAdditions: Time Changed!");
                    GetTimeSetClocks();
                }
                else if (SetByGUI)
                {
                    Debug.Log("RandomAdditions: Time Changed by Player!");
                    GetTimeSetClocks();
                }
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
