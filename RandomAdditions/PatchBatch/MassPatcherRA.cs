﻿using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Reflection;
using TerraTechETCUtil;
using RandomAdditions.PatchBatch;

namespace RandomAdditions
{
    internal class MassPatcherRA
    {
        internal static string modName => KickStart.ModName;
        internal static Harmony harmonyInst => KickStart.harmonyInstance;
        internal static string harmonyID => harmonyInst.Id;
        internal static bool IsUnstable = false;

        public static void CheckIfUnstable()
        {
            IsUnstable = SKU.DisplayVersion.Count(x => x == '.') > 2;
            DebugRandAddi.Log(modName + ": Is " + SKU.DisplayVersion + " an Unstable? - " + IsUnstable);
        }

        internal static bool TryOptimiseModLoadingSpeed = false;

        internal static bool MassPatchAll()
        {
            try
            {
                if (!KickStart.isNoBugReporterPresent)
                    harmonyInst.MassPatchAllWithin(typeof(BugReportPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(GlobalPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(UIPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(AllTankPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(AllProjectilePatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(ModulePatches), modName);
                if (KickStart.isNuterraSteamPresent)
                    harmonyInst.MassPatchAllWithin(typeof(NuterraPatches), modName);
                if (TryOptimiseModLoadingSpeed)
                    harmonyInst.MassPatchAllWithin(typeof(ModOptimizationPatches), modName);
                try
                {
                    harmonyInst.PatchAll(Assembly.GetExecutingAssembly());
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log(modName + ":Could not patch PatchBatch(Edge Cases) " + e);
                }

                return true;
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(modName + ": FAILED ON ALL PATCH ATTEMPTS - CASCADE FAILIURE " + e);
            }
            return false;
        }
        internal static bool MassUnPatchAll()
        {
            try
            {
                if (TryOptimiseModLoadingSpeed)
                    harmonyInst.MassUnPatchAllWithin(typeof(ModOptimizationPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(GlobalPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(UIPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(AllTankPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(AllProjectilePatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(ModulePatches), modName);
                if (KickStart.isNuterraSteamPresent)
                    harmonyInst.MassUnPatchAllWithin(typeof(NuterraPatches), modName);
                if (!KickStart.isNoBugReporterPresent)
                    harmonyInst.MassUnPatchAllWithin(typeof(BugReportPatches), modName);
                try
                {
                    harmonyInst.UnpatchAll(harmonyInst.Id);
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log(modName + ":Could not unpatch PatchBatch(Edge Cases) " + e);
                }
                return true;
            }
            catch (Exception e)
            {
                DebugRandAddi.Log(modName + ": FAILED ON ALL UN-PATCH ATTEMPTS - CASCADE FAILIURE " + e);
            }
            return false;
        }

    }
}
