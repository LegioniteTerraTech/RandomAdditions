using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RandomAdditions.RailSystem;
using HarmonyLib;

namespace RandomAdditions
{
    internal class ManRailsPatches
    {
        internal static class TechAudioPatches
        {
            internal static Type target = typeof(TechAudio);
            /// <summary>
            /// PatchTankToAllowLocoEngine
            /// </summary>
            [HarmonyPriority(-9001)]
            private static void GetWheelParams_Postfix(TechAudio __instance, ref TechAudio.UpdateAudioCache cache)
            {
                var train = __instance.GetComponent<TankLocomotive>();
                if (train && train.BogieMaxDriveForce > 0)
                {
                    cache.wheelsTotalNum += train.ModuleBogiesCount;
                    cache.wheelsGroundedNum += train.EngineBlockCount;
                    cache.wheelsGroundedPerType[(int)TechAudio.WheelTypes.MetalWheel] += train.ActiveBogieCount;
                }
            }

            [HarmonyPriority(-9001)]
            private static bool OnControlInput_Prefix(TechAudio __instance, ref TankControl.ControlState data,
                 ref float ___m_Drive, ref float ___m_Turn)
            {
                ___m_Drive = (data.InputMovement + data.Throttle).magnitude;
                ___m_Turn = data.InputRotation.magnitude;
                return false;
            }
        }
    }
}
