using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using RandomAdditions.RailSystem;

namespace RandomAdditions
{
    internal class AllTankPatches : MassPatcherRA
    {
        // Major Patches
        internal static class TankPatches
        {
            internal static Type target = typeof(Tank);
            /// <summary>
            /// PatchTankToHelpClocks
            /// </summary>
            private static void OnPool_Postfix(Tank __instance)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Tank OnPool(TimeTank)");
                var ModuleAdd2 = __instance.gameObject.AddComponent<RandomTank>();
                ModuleAdd2.Initiate();
            }

            private static void NotifyDamage_Postfix(Tank __instance, ref ManDamage.DamageInfo info, ref TankBlock blockDamaged)
            {
                RandomTank.Insure(__instance).OnDamaged(info, blockDamaged);
            }
        }

        internal static class TankControlPatches
        {
            internal static Type target = typeof(TankControl);
            /// <summary>
            /// PatchTankToHelpEvasion
            /// </summary>
            private static void GetWeaponTargetLocation_Postfix(TankControl __instance, ref Vector3 __result, ref Vector3 origin)
            {
                if (origin == __result)
                    return;
                TankDestraction TD = __instance.GetComponent<TankDestraction>();
                if (TD)
                {
                    __result = TD.GetPosDistract(__result);
                }
            }
        }
    }
}
