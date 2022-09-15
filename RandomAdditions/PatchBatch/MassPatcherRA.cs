using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using TerraTechETCUtil;

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

        internal static bool MassPatchAll()
        {
            try
            {
                harmonyInst.MassPatchAllWithin(typeof(GlobalPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(AllTankPatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(AllProjectilePatches), modName);
                harmonyInst.MassPatchAllWithin(typeof(ModulePatches), modName);

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
                harmonyInst.MassUnPatchAllWithin(typeof(GlobalPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(AllTankPatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(AllProjectilePatches), modName);
                harmonyInst.MassUnPatchAllWithin(typeof(ModulePatches), modName);

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
