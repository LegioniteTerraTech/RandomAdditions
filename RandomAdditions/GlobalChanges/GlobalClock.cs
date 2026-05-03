using System;
using System.Collections.Generic;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    public class GlobalClock : MonoBehaviour// The clock that handles all clocks
    {
        //Global Variables
        public static bool SetByGUI = false;
        public static bool LockTime = false;
        public static int SavedTime = 0;// The Hour to keep the world set to 
        internal static int LastHour = 0;     // Last Hour 
        public static HashSet<TankClock> tanks = new HashSet<TankClock>();
        public static bool UiDirty = false;

        public static bool TimeControllerPresent = false;// Is the time locked to a master block?

        private static GlobalClock inst;
        //The global timekeeper for all clocks
        //  think of it as the "atomic clock" of the offworld.
        //  Also handles setting the time and then locking it.
        //   Also handles static updating of various other modules.
        internal static void Initiate()
        {
            if (inst)
                return;
            inst = new GameObject("GlobalClockGeneral").AddComponent<GlobalClock>();
            tanks = new HashSet<TankClock>();
            DebugRandAddi.Log("RandomAdditions: Created GlobalClock.");
            inst.gameObject.SetActive(true);
            inst.enabled = true;
            ManTechs.inst.TankTeamChangedEvent.Subscribe(SanityCheck);
        }
        internal static void DeInit()
        {
            if (!inst)
                return;
            ManTechs.inst.TankTeamChangedEvent.Unsubscribe(SanityCheck);
            tanks = null;
            Destroy(inst.gameObject);
            inst = null;
            DebugRandAddi.Log("RandomAdditions: DeInit GlobalClock.");
        }
        private static void SanityCheck(Tank tankGet, ManTechs.TeamChangeInfo TCI)
        {
            if (tankGet?.GetComponent<TankClock>() is TankClock TC)
            {
                if (TCI.m_OldTeam == ManPlayer.inst.PlayerTeam)
                    UiDirty = true;
            }
        }


        //All ModuleClock(s) will control the time based on global values.
        //  The latest Techs will override this however.

        public void GetTimeSetClocks()
        {
            LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
            TimeControllerPresent = UpdateClockVisuals();
            if (TimeControllerPresent && LockTime)
            {
                //DebugRandAddi.Log("RandomAdditions: Time Controller present.");
                if (SetByGUI)
                {
                    SetAllTimeControllers();
                    LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                    UpdateClockVisuals();
                }
                else
                {
                    GetFirstTimeControllerAndSetTime();
                    LastHour = Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay;
                    UpdateClockVisuals();
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
            SavedTime = -1;
            foreach (TankClock clunk in tanks)
                clunk.GetSaveStateClocks(ref SavedTime);
            if (SavedTime != -1)
                SetTime();
        }
        public void SetAllTimeControllers()
        {
            foreach (TankClock clunk in tanks)
                clunk.SetSaveStateClocks(SavedTime);
            SetTime();
        }
        public void SetTime()
        {
            DebugRandAddi.Info("RandomAdditions: Time Change external queued!");
            if (ManTimeOfDay.inst.TimeOfDay != SavedTime)
                Singleton.Manager<ManTimeOfDay>.inst.SetTimeOfDay(SavedTime, 0, 0, false);
            DebugRandAddi.Info("RandomAdditions: Time Changed to " + Singleton.Manager<ManTimeOfDay>.inst.TimeOfDay);
        }

        public bool UpdateClockVisuals()
        {
            bool timeControl = false;
            foreach (TankClock clunk in tanks)
                timeControl |= clunk.UpdateClockVisuals();
            return timeControl;
        }

        public void Update()
        {
            if (ManTimeOfDay.inst.TimeOfDay != SavedTime)
            {
                DebugRandAddi.Info("RandomAdditions: Time Changed!");
                GetTimeSetClocks();
            }
            else if (UiDirty)
            {
                GetTimeSetClocks();
            }
            else if (SetByGUI)
            {
                DebugRandAddi.Log("RandomAdditions: Time Changed by Player!");
                GetTimeSetClocks();
            }
            UiDirty = false;
        }

        internal class GUIManaged
        {
            private static bool display = false;

            public static void GUIGetTotalManaged()
            {
                GUILayout.Box("---- Global Clock --- ");
                display = AltUI.Toggle(display, "Show: ");
                if (display)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Time[HOUR]: ");
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(LastHour.ToString());
                    GUILayout.EndHorizontal();
                }
            }
        }
    }
}
