using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CustomModules.LegacyModule;
using HarmonyLib;

namespace RandomAdditions.PatchBatch
{
    internal class NuterraPatches
    {
        internal static class ModuleCustomBlockPatches
        {
            internal static Type target = typeof(ModuleCustomBlock);
            internal static FieldInfo targetTime = typeof(ModuleCustomBlock).GetField("emissionTimeDelay", BindingFlags.Instance | BindingFlags.NonPublic);

            [HarmonyPriority(-9001)]
            private static void Update_Postfix(ModuleCustomBlock __instance)
            {
                if (__instance != null && (float)targetTime.GetValue(__instance) <= 0)
                {   // SLEEP and save valuable CPU cycles!
                    __instance.enabled = false;
                }
            }
            [HarmonyPriority(-9001)]
            private static void ChangeTimeEmission_Postfix(ModuleCustomBlock __instance)
            {   // Wake it up!
                if (__instance != null)
                    __instance.enabled = true;
            }
        }
    }
}
