﻿using System;
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
        internal static class TechAudioPatches
        {
            internal static Type target = typeof(TechAudio);
            /// <summary>
            /// PatchTankToAllowLocoEngine
            /// </summary>
            private static void GetWheelParams_Postfix(TechAudio __instance, ref TechAudio.UpdateAudioCache cache)
            {
                var train = __instance.GetComponent<TankLocomotive>();
                if (train && train.BogieMaxDriveForce > 0)
                {
                    cache.wheelsTotalNum += train.BogieCount;
                    cache.wheelsGroundedNum += train.EngineBlockCount;
                    cache.wheelsGroundedPerType[(int)TechAudio.WheelTypes.MetalWheel] += train.ActiveBogieCount;
                }
            }

            private static FieldInfo dr = typeof(TechAudio).GetField("m_Drive", BindingFlags.NonPublic | BindingFlags.Instance);
            private static FieldInfo tr = typeof(TechAudio).GetField("m_Turn", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool OnControlInput_Prefix(TechAudio __instance, ref TankControl.ControlState data)
            {
                dr.SetValue(__instance, (data.InputMovement + data.Throttle).magnitude);
                tr.SetValue(__instance, data.InputRotation.magnitude);
                return false;
            }
        }
    }
}
