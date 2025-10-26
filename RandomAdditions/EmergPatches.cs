using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using SafeSaves;

namespace RandomAdditions
{
    [AutoSaveManager]
    public class EmergPatches
    {
        [SSManagerInst]
        public static EmergPatches inst = new EmergPatches();

        [SSaveField]
        public int RaDSizeLimit = 0;

        internal static bool HasSizeLimitSet() => inst != null && inst.RaDSizeLimit >= 64;
        internal static void TrySaveAndLockSizeLimit(int value = 0)
        {
            if (ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD && inst.RaDSizeLimit == 0)
            {
                inst.RaDSizeLimit = value >= 64 ? value : ManSpawn.inst.BlockLimit;
                DebugRandAddi.Log("EmergPatchSizeLimit - Saved and locked BuildSizeLimit as " + inst.RaDSizeLimit);
            }
        }

        internal static void PrepareForSaving()
        {
            TrySaveAndLockSizeLimit();
        }
        internal static void FinishedSaving()
        {
        }
        internal static void PrepareForLoading()
        {
            if (inst != null)
                inst.RaDSizeLimit = 0;
        }
        internal static void FinishedLoading()
        {
            if (ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD)
            {
                try
                {
                    if (HasSizeLimitSet())
                    {
                        Singleton.Manager<ManGameMode>.inst.AddModeInitSetting("BuildSizeLimit", inst.RaDSizeLimit);
                        ManSpawn.inst.BlockLimit = inst.RaDSizeLimit;
                        DebugRandAddi.Log("EmergPatchSizeLimit - Resync BuildSizeLimit to " + inst.RaDSizeLimit);
                    }
                    else
                    {
                        Singleton.Manager<ManGameMode>.inst.AddModeInitSetting("BuildSizeLimit", 256);
                        ManSpawn.inst.BlockLimit = 256;
                        DebugRandAddi.Assert("EmergPatchSizeLimit - WE SHALL DEFAULT TO 256 TO PREVENT TECH LOSS!!!");
                        TrySaveAndLockSizeLimit(256);
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("EmergPatchSizeLimit - error " + e);
                }
            }
        }
    }
}
