using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    internal static class Patches
    {



        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // Custom Block Modules


        // Trying to change this will put a huge strain on the game
        /*
        [HarmonyPatch(typeof(ManWheels.Wheel))]
        [HarmonyPatch("MainThread_PostUpdate")]
        private static class TryMakeRotateAccurate
        {
            private static void Postfix(ManWheels.Wheel __instance, ref int __result)
            {
                if (__instance.wheelParams.strafeSteeringSpeed > 0f)
                {
                    float f = -90f * strafing * ((float)Math.PI / 180f);
                    float num3 = Mathf.Sin(f);
                    float m = (s_SteerRotMat.m00 = Mathf.Cos(f));
                    __instance.s_SteerRotMat.m02 = num3;
                    __instance.s_SteerRotMat.m20 = 0f - num3;
                    __instance.s_SteerRotMat.m22 = m;
                    __instance.tireFrame.SetRotationIfChanged((tireFrameMatrix * s_SteerRotMat).rotation);
                }
            }
        }*/

        /*
        [HarmonyPatch(typeof(Button))]
        [HarmonyPatch("Press")]
        private static class TrackButtonPressesToFindThings
        {
            private static void Postfix(Button __instance)
            {
                DebugRandAddi.Log("--------------------------------------------");
                if (__instance.transform.parent)
                {
                    DebugRandAddi.Log("Button " + __instance.name + " has parent, hierachy - " + 
                        Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.transform.parent.gameObject, "- "));
                }
                else
                {
                    DebugRandAddi.Log("Button " + __instance.name + " hierachy - " +
                        Nuterra.NativeOptions.UIUtilities.GetComponentTree(__instance.gameObject, "- "));
                }
                DebugRandAddi.Log("--------------------------------------------\n");
            }
        }
        //*/





    }
}
