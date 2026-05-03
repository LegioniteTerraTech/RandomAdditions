using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    internal class CircuitPatches
    {
        // For C&S
        private static bool CnCDispWakeup = false;
        private static ExtUsageHint.UsageHint warningCnCDisabled = new ExtUsageHint.UsageHint(KickStart.ModID,
            KickStart.ModID + ".cnsOffWarn", new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
            {
                { LocalisationEnums.Languages.US_English, "A Circuits block was attached somewhere \"Circuits\" is disabled in mod settings. You may want to turn it back on." }
            }), 6, true);
        internal static class ModuleCircuitDispensorPatches
        {
            internal static Type target = typeof(ModuleCircuitDispensor);

            [HarmonyPriority(-9001)]
            internal static void SendChargeToOutputs_Prefix(ModuleCircuitDispensor __instance, ref int strength)
            {
                var hook = __instance.GetComponent<ModuleCircuitExt>();
                if (hook && hook.OutCharge > strength)
                {
                    strength = hook.OutCharge;
                }
            }
            internal static bool OnAttached_Prefix(ModuleCircuitDispensor __instance, ICircuitDispensor[] ___m_Dispensors)
            {
                if (KickStart.noCircuits && ___m_Dispensors.Any())
                    warningCnCDisabled.Show();

                if (!KickStart.smrtCircuits || CnCDispWakeup)
                    return true;

                DispensorCompliment DC = __instance.GetComponent<DispensorCompliment>();
                if (!DC)
                {
                    DC = __instance.gameObject.AddComponent<DispensorCompliment>();
                    DC.Sub(__instance);
                }
                DC.WeAttached();
                return false;
            }
            internal static bool OnDetaching_Prefix(ModuleCircuitDispensor __instance)
            {
                if (!KickStart.smrtCircuits || CnCDispWakeup)
                    return true;
                __instance.GetComponent<DispensorCompliment>()?.WeDetaching();
                return false;
            }
            internal class DispensorCompliment : MonoBehaviour
            {
                private ModuleCircuitDispensor Main;
                private int InteractingCount = 0;
                private static MethodInfo doAttach = AccessTools.Method(typeof(ModuleCircuitDispensor), "OnAttached");
                private static MethodInfo doDetach = AccessTools.Method(typeof(ModuleCircuitDispensor), "OnDetaching");
                public void Sub(ModuleCircuitDispensor main)
                {
                    Main = main;
                    Main.block.NeighbourAttachingEvent.Subscribe(OnCnSAttached);
                    Main.block.NeighbourDetachingEvent.Subscribe(OnCnSDetached);
                }

                internal void WeAttached()
                {
                    InteractingCount = 0;
                    Main.block.ForeachConnectedBlock(OnCnSAttached);
                }
                internal void WeDetaching()
                {
                    Main.block.ForeachConnectedBlock(OnCnSDetached);
                    if (KickStart.smrtCircuits && InteractingCount != 0)
                        DebugRandAddi.LogPopupToPlayer(gameObject.name + " failed to detach properly " +
                            StackTraceUtility.ExtractStackTrace(), true);
                    InteractingCount = 0;
                }
                private void OnCnSAttached(TankBlock newNeighboor)
                {
                    if (KickStart.smrtCircuits && newNeighboor?.CircuitNode && newNeighboor.CircuitNode.ChargeInPoints.Any())
                    {
                        if (InteractingCount == 0)
                        {
                            try
                            {
                                CnCDispWakeup = true;
                                doAttach.Invoke(Main, Array.Empty<object>());
                            }
                            finally
                            {
                                CnCDispWakeup = false;
                            }
                        }
                        InteractingCount++;
                    }
                }
                private void OnCnSDetached(TankBlock leavingNeighboor)
                {
                    if (KickStart.smrtCircuits && leavingNeighboor?.CircuitNode && leavingNeighboor.CircuitNode.ChargeInPoints.Any())
                    {
                        InteractingCount--;
                        if (InteractingCount == 0)
                        {
                            try
                            {
                                CnCDispWakeup = true;
                                doDetach.Invoke(Main, Array.Empty<object>());
                            }
                            finally
                            {
                                CnCDispWakeup = false;
                            }
                        }
                    }
                }
            }
        }
        /*
        internal static class TechCircuitsPatches
        {
            internal static Type target = typeof(TechCircuits);
            private static List<TechCircuits>

            [HarmonyPriority(-9001)]
            internal static void PropagateHighestNetworkChargesToLinkedNetworksThenReleaseCharges_Prefix(TechCircuits __instance,
                m_SortedNetworks)
            {
                var hook = __instance.GetComponent<ModuleCircuitExt>();
                if (hook && hook.OutCharge > strength)
                {
                    strength = hook.OutCharge;
                }
            }
        }*/

        internal static class ModuleCircuitReceiverPatches
        {
            internal static Type target = typeof(ModuleCircuitReceiver);
            private static MethodInfo doSpawn = AccessTools.Method(typeof(ModuleCircuitReceiver), "OnSpawn");
            private static MethodInfo doRecycle = AccessTools.Method(typeof(ModuleCircuitReceiver), "OnRecycle");
            internal static bool OnSpawn_Prefix(ModuleCircuitReceiver __instance)
            {
                if (!KickStart.smrtCircuits || CnCDispWakeup)
                    return true;
                RecieverCompliment DC = __instance.GetComponent<RecieverCompliment>();
                if (!DC)
                {
                    DC = __instance.gameObject.AddComponent<RecieverCompliment>();
                    DC.Sub(__instance);
                }
                return false;
            }
            internal static bool OnRecycle_Prefix(ModuleCircuitReceiver __instance)
            {
                if (!KickStart.smrtCircuits || CnCDispWakeup)
                    return true;
                return false;
            }
            internal class RecieverCompliment : MonoBehaviour
            {
                private ModuleCircuitReceiver Main;
                private int InteractingCount = 0;
                public void Sub(ModuleCircuitReceiver main)
                {
                    Main = main;
                    Main.block.AttachedEvent.Subscribe(WeAttached);
                    Main.block.DetachingEvent.Subscribe(WeDetaching);
                    Main.block.NeighbourAttachingEvent.Subscribe(OnCnSAttached);
                    Main.block.NeighbourDetachingEvent.Subscribe(OnCnSDetached);
                }

                internal void WeAttached()
                {
                    InteractingCount = 0;
                    Main.block.ForeachConnectedBlock(OnCnSAttached);
                }
                internal void WeDetaching()
                {
                    Main.block.ForeachConnectedBlock(OnCnSDetached);
                    if (KickStart.smrtCircuits && InteractingCount != 0)
                        DebugRandAddi.LogPopupToPlayer(gameObject.name + " failed to detach properly " +
                            StackTraceUtility.ExtractStackTrace(), true);
                    InteractingCount = 0;
                }
                private void OnCnSAttached(TankBlock newNeighboor)
                {
                    if (KickStart.smrtCircuits && newNeighboor?.CircuitNode && newNeighboor.CircuitNode.ChargeOutPoints.Any())
                    {
                        if (InteractingCount == 0)
                        {
                            try
                            {
                                CnCDispWakeup = true;
                                doSpawn.Invoke(Main, Array.Empty<object>());
                            }
                            finally
                            {
                                CnCDispWakeup = false;
                            }
                        }
                        InteractingCount++;
                    }
                }
                private void OnCnSDetached(TankBlock leavingNeighboor)
                {
                    if (KickStart.smrtCircuits && leavingNeighboor?.CircuitNode && leavingNeighboor.CircuitNode.ChargeOutPoints.Any())
                    {
                        InteractingCount--;
                        if (InteractingCount == 0)
                        {
                            try
                            {
                                CnCDispWakeup = true;
                                doRecycle.Invoke(Main, Array.Empty<object>());
                            }
                            finally
                            {
                                CnCDispWakeup = false;
                            }
                        }
                        else if (InteractingCount < 0)
                            DebugRandAddi.LogPopupToPlayer(gameObject.name + " failed to detach properly(2) " +
                                StackTraceUtility.ExtractStackTrace(), true);
                    }
                }
            }
        }
    }
}
