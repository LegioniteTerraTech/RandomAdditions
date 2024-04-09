using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomAdditions
{
    internal class CircuitPatches : MassPatcherRA
    {
        internal static class ModuleCircuitDispensorPatches
        {
            internal static Type target = typeof(ModuleCircuitDispensor);

            [HarmonyPriority(-9001)]
            private static void SendChargeToOutputs_Prefix(ModuleCircuitDispensor __instance, ref int strength)
            {
                var hook = __instance.GetComponent<ModuleCircuitExt>();
                if (hook && hook.OutCharge > strength)
                {
                    strength = hook.OutCharge;
                }
            }
        }
    }
}
