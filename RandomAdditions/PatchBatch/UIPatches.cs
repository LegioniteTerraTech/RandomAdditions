using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RandomAdditions.Minimap;
using RandomAdditions.RailSystem;
using TerraTechETCUtil;
using TMPro;
using UnityEngine;

namespace RandomAdditions.PatchBatch
{
    internal class UIPatches
    {

        internal static class ManUIPatches
        {
            internal static Type target = typeof(ManUI);

            /// <summary>
            /// Make it MORE legible, because it's irritating to deal with errors and have it stretch beyond the viewable screen
            /// </summary>
            [HarmonyPriority(-4500)]
            internal static bool ShowErrorPopup_Prefix(ref string text)
            {
                ManModGUI.ShowErrorPopup(text, true);
                return false;
            }

        }
        internal static class CursorPatches
        {
            internal static Type target = typeof(GameCursor);

            /// <summary>
            /// See CursorChanger for more information
            /// </summary>
            /// <param name="__result"></param>
            [HarmonyPriority(-4500)]
            internal static void GetCursorState_Postfix(ref GameCursor.CursorState __result)
            {
                if (!CursorChanger.AddedNewCursors)
                    return;
                if (ManRails.SelectedNode != null)
                {
                    __result = CursorChanger.CursorIndexCache[0];
                }
            }

        }


        internal static class ManRadarPatches
        {
            internal static Type target = typeof(ManRadar);
            private static bool TryGetCustomIcon(ManRadar.IconType iconType, out ManRadar.IconEntry output)
            {
                output = default;
                if ((int)iconType >= ManMinimapExt.VanillaMapIconCount)
                {   // Custom icon get
                    if (ManMinimapExt.addedIcons.TryGetValue(iconType, out output))
                        return true;
                    throw new ArgumentException("Icon type of " + iconType.ToString() + " does not exist in ManMinimapExt");
                }
                return false;
            }
            // Allow custom types on UI
            [HarmonyPriority(-9001)]
            internal static bool GetIconElementPrefab_Prefix(ManRadar __instance, ref UIMiniMapElement __result, ManRadar.IconType iconType)
            {
                if (TryGetCustomIcon(iconType, out var outp))
                {
                    if (outp.mapIconPrefab == null)
                        throw new NullReferenceException("Failed to get custom map UI element instance");
                    __result = outp.mapIconPrefab;
                    return false;
                }
                return true;
            }

            [HarmonyPriority(-9001)]
            internal static bool GetIconColor_Prefix(ManRadar __instance, ref Color __result, ManRadar.IconType iconType)
            {
                if (TryGetCustomIcon(iconType, out var outp))
                {
                    __result = outp.colour;
                    return false;
                }
                return true;
            }

            [HarmonyPriority(-9001)]
            internal static bool GetCountDisplayingPastRange_Prefix(ManRadar __instance, ref int __result, ManRadar.IconType iconType)
            {
                try
                {
                    if (TryGetCustomIcon(iconType, out var outp))
                    {
                        __result = outp.numDisplayingAtRange;
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    __result = 0;
                    return false;
                }
                return true;
            }
            [HarmonyPriority(-9001)]
            internal static bool CheckIconRotates_Prefix(ManRadar __instance, ref bool __result, ManRadar.IconType iconType)
            {
                if (TryGetCustomIcon(iconType, out var outp))
                {
                    __result = outp.offMapRotates;
                    return false;
                }
                return true;
            }
            [HarmonyPriority(-9001)]
            internal static bool GetPriority_Prefix(ManRadar __instance, ref float __result, ManRadar.IconType iconType)
            {
                if (TryGetCustomIcon(iconType, out var outp))
                {
                    __result = outp.priority;
                    return false;
                }
                return true;
            }
        }

        internal static class UIMiniMapLayerTechPatches
        {
            internal static Type target = typeof(UIMiniMapLayerTech);
            // Allow custom radar markers
            [HarmonyPriority(-9001)]
            internal static bool TryGetIconForTrackedVisiblePrefix(UIMiniMapLayerTech __instance, 
                bool isQuestMarker, bool isManualTarget, TrackedVisible trackedVisible, 
                ref ManRadar.IconType outIconType)
            {
                if (isQuestMarker || isManualTarget || ManMinimapExt.AddedMinimapIndexes == 0)
                    return true;
                foreach (var item in ManMinimapExt.iconConditions)
                {
                    if (item.Key(trackedVisible))
                    {
                        outIconType = item.Value;
                        return false;
                    }
                }
                return true;
            }

        }
        internal static class UIMiniMapDisplayPatches
        {
            internal static Type target = typeof(UIMiniMapDisplay);
            // Allow train tracks on UI
            [HarmonyPriority(-9001)]
            internal static void Show_Postfix(UIMiniMapDisplay __instance)
            {
                if ((bool)__instance)
                {
                    if (__instance.GetComponent<ManMinimapExt.MinimapExt>())
                        return;
                    var instWorld = __instance.gameObject.AddComponent<ManMinimapExt.MinimapExt>();
                    instWorld.InitInst(__instance);
                }
            }

        }

        internal static class TankCameraPatches
        {
            internal static Type target = typeof(TankCamera);

            static FieldInfo shaker = typeof(TankCamera).GetField("m_CameraShakeTimeRemaining", BindingFlags.NonPublic | BindingFlags.Instance);
            /// <summary>
            /// GetOutOfHereCameraShakeShack
            /// </summary>=
            [HarmonyPriority(-9001)]
            internal static bool SetCameraShake_Prefix(TankCamera __instance)
            {
                if (KickStart.NoShake)
                {
                    //DebugRandAddi.Log("RandomAdditions: Stopping irritation.");
                    shaker.SetValue(__instance, 0);
                    return false;
                }
                return true;
            }
        }
        internal static class PlayerFreeCameraPatches
        {
            internal static Type target = typeof(PlayerFreeCamera);

            const float newMaxFreeCamRange = 2950;// 250
            static float newMPMaxFreeCamRange = ManWorld.inst.TileSize * 3.25f;//4.5f;//250;

            /// <summary>
            /// MORE_RANGE
            /// </summary>
            static FieldInfo overr = typeof(PlayerFreeCamera).GetField("maxDistanceFromTank", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo underr = typeof(PlayerFreeCamera).GetField("groundClearance", BindingFlags.NonPublic | BindingFlags.Instance);
            internal static void Enable_Prefix(PlayerFreeCamera __instance)
            {
                if (ManNetwork.IsNetworked)
                    overr.SetValue(__instance, newMPMaxFreeCamRange);    // max "safe" range - 2.5x vanilla
                else
                    overr.SetValue(__instance, newMaxFreeCamRange);    // max "safe" range - You are unlikely to hit the limit
                underr.SetValue(__instance, -25f);   // cool fights underground - You can do this in Build Beam so it makes sense
                DebugRandAddi.Info("RandomAdditions: EXTENDED Free cam range");
            }

        }


        internal static class UIDamageTypeDisplayPatches
        {
            internal static Type target = typeof(UIDamageTypeDisplay);

            static FieldInfo type = typeof(UIDamageTypeDisplay).GetField("m_DisplayType", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo name = typeof(UIDamageTypeDisplay).GetField("m_NameText", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo dispName = typeof(UIDamageTypeDisplay).GetField("m_Tooltip", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPriority(-9001)]
            internal static bool SetBlock_Prefix(UIDamageTypeDisplay __instance, ref BlockTypes blockType)
            {
                if ((int)type.GetValue(__instance) == 2)
                {
                    var blockInst = ManSpawn.inst.GetBlockPrefab(blockType);
                    if (blockInst)
                    {
                        //DebugRandAddi.Log("SetBlock_Prefix - set for " + blockType.ToString());

                        try
                        {
                            var modDamageable = blockInst.GetComponent<ModuleReinforced>();
                            if (modDamageable != null)
                            {
                                if (!modDamageable.CustomDamagableName.NullOrEmpty())
                                {
                                    __instance.Set(modDamageable.CustomDamagableIcon);
                                    var val = (TextMeshProUGUI)name.GetValue(__instance);
                                    if (val)
                                    {
                                        val.text = modDamageable.CustomDamagableName;
                                        //DebugRandAddi.Log("set damageable name to " + modDamageable.CustomDamagableName);
                                    }
                                    else
                                        DebugRandAddi.Log("set SetBlock_Prefix name failed because TextMeshProUGUI null");
                                    var namD = (TooltipComponent)dispName.GetValue(__instance);
                                    if (namD)
                                    {
                                        namD.SetText(modDamageable.CustomDamagableName);
                                    }
                                    else
                                        DebugRandAddi.Log("set SetBlock_Prefix name failed because TooltipComponent null");
                                    return false;
                                }
                                else
                                    DebugRandAddi.Log("set SetBlock_Prefix name failed because CustomDamagableName null/empty");
                            }
                            /*
                            else
                            {
                                DebugRandAddi.Log("SetBlock_Prefix - no ModuleReinforced for " + blockType.ToString());
                                foreach (var item in blockInst.GetComponents<MonoBehaviour>())
                                {
                                    DebugRandAddi.Log(" - " + item.GetType().ToString());
                                }
                            }*/
                        }
                        catch (Exception e)
                        {
                            DebugRandAddi.Log("SetBlock_Prefix - ERROR ~ " + e);
                        }
                    }
                    else
                        DebugRandAddi.Log("SetBlock_Prefix - not set for " + blockType.ToString() + ", no prefab!");
                }
                return true;
            }
        }

        internal static class ManHUDPatches
        {
            internal static Type target = typeof(ManHUD);

            [HarmonyPriority(-9001)]
            internal static bool AddRadialOpenRequest_Prefix(ManHUD __instance, ref ManHUD.HUDElementType elementType)
            {
                return elementType != UIHelpersExt.customElement;
            }
        }



    }
}
