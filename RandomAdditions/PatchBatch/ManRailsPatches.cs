using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RandomAdditions.RailSystem;

namespace RandomAdditions
{
    internal class ManRailsPatches : MassPatcherRA
    {
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
                    cache.wheelsTotalNum += train.ModuleBogiesCount;
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
