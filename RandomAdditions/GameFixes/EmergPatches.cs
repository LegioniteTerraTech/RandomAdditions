using System;
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
            if (inst == null)
                inst = new EmergPatches();
            try
            {
                if (ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD && inst.RaDSizeLimit == 0)
                {
                    inst.RaDSizeLimit = value >= 64 ? value : ManSpawn.inst.BlockLimit;
                    DebugRandAddi.Log("EmergPatchSizeLimit - Saved and locked BuildSizeLimit as " + inst.RaDSizeLimit);
                }
            }
            catch (Exception e)
            {
                DebugRandAddi.LogError("EmergPatchSizeLimit - error on call TrySaveAndLockSizeLimit() " + e);
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

        /// <summary>
        /// delay till AFTER they set to prevent mem overload
        /// </summary>
        internal static void ApplySettings()
        {
            if (ManGameMode.inst.GetCurrentGameType() == ManGameMode.GameType.RaD)
            {
                try
                {
                    if (HasSizeLimitSet() && KickStart.OverrideTechMax)
                    {
                        Singleton.Manager<ManGameMode>.inst.AddModeInitSetting("BuildSizeLimit", inst.RaDSizeLimit);
                        ManSpawn.inst.BlockLimit = inst.RaDSizeLimit;
                        DebugRandAddi.Log("EmergPatchSizeLimit - Resync BuildSizeLimit to " + inst.RaDSizeLimit);
                    }
                    else
                    {
                        int limitSet;
                        switch (KickStart.SaveMyTechMax)
                        {
                            case 1:
                                limitSet = 128;
                                break;
                            case 2:
                                limitSet = 256;
                                break;
                            default:
                                limitSet = 64;
                                break;
                        }
                        Singleton.Manager<ManGameMode>.inst.AddModeInitSetting("BuildSizeLimit", limitSet);
                        ManSpawn.inst.BlockLimit = limitSet;
                        DebugRandAddi.Assert("EmergPatchSizeLimit - WE SHALL DEFAULT TO " + limitSet + " TO PREVENT TECH LOSS!!!");
                        TrySaveAndLockSizeLimit(limitSet);
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("EmergPatchSizeLimit - error " + e);
                }
            }
        }
        internal static void FinishedLoading()
        {
            if (inst == null)
                inst = new EmergPatches();
            try
            {
                /**/
            }
            catch (Exception e2)
            {
                DebugRandAddi.LogError("EmergPatchSizeLimit - error on BASE INIT " + e2);
            }
        }
    }
}
