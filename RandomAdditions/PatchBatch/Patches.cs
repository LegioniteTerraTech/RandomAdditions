using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using FMOD.Studio;
using RandomAdditions.Minimap;
using TerraTechETCUtil;
using TMPro;

namespace RandomAdditions
{
    internal static class Patches
    {
        [HarmonyPatch(typeof(UITechManagerEntry))]
        [HarmonyPatch("Init")]//
        internal static class GrabUIEntryDetails
        {
            const string name = "JumpTo_Button";
            static FieldInfo TargetInfo = typeof(UITechManagerEntry).GetField("m_TrackedVis", BindingFlags.Instance | BindingFlags.NonPublic);
            static Material mockIcon = null;
            private static LocExtStringMod LOC_DirectControl = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Assume Direct Control"},
            { LocalisationEnums.Languages.Japanese, "をコントロールする"},
        });
            private static LocExtStringMod LOC_DirectControlNotAvail = new LocExtStringMod(new Dictionary<LocalisationEnums.Languages, string>()
        {
            { LocalisationEnums.Languages.US_English, "Cannot Assume Direct Control"},
            { LocalisationEnums.Languages.Japanese, "制御できない"},
        });
            internal static void Postfix(UITechManagerEntry __instance, TrackedVisible tv)
            {
                Transform MainTrans = __instance.transform;
                Button[] ToCopy = __instance.gameObject.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < ToCopy.Length; i++)
                {
                    Button button = ToCopy[i];
                    Transform parent2 = button.transform.parent;
                    if (MainTrans == parent2)
                        continue;
                    if (parent2.name == name)
                    {
                        return;
                    }
                }
                DebugRandAddi.Log("Creating new " + name);
                Transform template = null;
                int locatorIndex = 0;
                for (int i = 0; i < ToCopy.Length; i++)
                {
                    Button button = ToCopy[i];
                    Transform parent2 = button.transform.parent;
                    if (MainTrans == parent2 || !parent2.name.Contains("_Button"))
                        continue;
                    if (template == null && parent2.name.Contains("SendToSCU"))
                        template = parent2;
                    DebugRandAddi.Log(parent2.name + " posLoc: " + parent2.localPosition.ToString());
                    parent2.localPosition = new Vector3(Mathf.Lerp(244f, 454f, locatorIndex / 4f), 0.6f, 0.0f); 
                    locatorIndex++;
                }
                Transform parentMain = __instance.transform;
                GameObject GO = UnityEngine.Object.Instantiate(template.gameObject, parentMain);
                if (GO == null)
                    throw new NullReferenceException("GO");
                Button newButton = GO.GetComponentInChildren<Button>();
                if (newButton == null)
                    throw new NullReferenceException("newButton");
                Image[] newImages = GO.GetComponentsInChildren<Image>();
                if (newImages == null)
                    throw new NullReferenceException("newImage");
                TooltipComponent newTooltip = GO.GetComponentInChildren<TooltipComponent>();
                if (newTooltip == null)
                    throw new NullReferenceException("newTooltip");
                bool canJump = ManMinimapExt.CanTeleportSafely(tv);
                newButton.interactable = canJump;
                if (canJump)
                {
                    LOC_DirectControl.SetTextAuto(newTooltip);
                    newTooltip.SetMode(UITooltipOptions.Default);
                }
                else
                {
                    LOC_DirectControlNotAvail.SetTextAuto(newTooltip);
                    newTooltip.SetMode(UITooltipOptions.Warning);
                }
                GO.name = name;
                GO.transform.localPosition = new Vector3(Mathf.Lerp(244f, 454f, locatorIndex / 4f), 0.6f, 0.0f);//404.0f
                Button.ButtonClickedEvent BCE = new Button.ButtonClickedEvent();
                BCE.AddListener(() =>
                {
                    TrackedVisible TV = (TrackedVisible)TargetInfo.GetValue(__instance);
                    ManMinimapExt.MinimapExt.TryJumpPlayer(TV);
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                });
                newButton.onClick = BCE;
                if (mockIcon == null)
                {
                    mockIcon = new Material(newImages[0].material.shader);
                    mockIcon.mainTexture = UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1").texture;
                }
                foreach (Image image in newImages)
                {
                    // SETTING IMAGE DOES NOT WORK FOR SOME STUPID REASON
                    image.material = mockIcon;
                    CanvasRenderer CR = image.GetComponent<CanvasRenderer>();
                    CR.SetPopMaterial(mockIcon, 0);
                    CR.SetMaterial(mockIcon, 0);
                    image.SetAllDirty();
                    DebugRandAddi.Log("Layer " + image.name);
                    if (!image.name.Contains("Button"))
                    { 
                        UnityEngine.Object.Destroy(image.gameObject);
                    }
                }
                // SETTING IMAGE DOES NOT WORK FOR SOME STUPID REASON
                //Utilities.LogGameObjectHierachy(newButton.gameObject);
                newButton.image.material = mockIcon;
                CanvasRenderer CR2 = newButton.image.GetComponent<CanvasRenderer>();
                CR2.SetPopMaterial(mockIcon, 0);
                CR2.SetMaterial(mockIcon, 0);
                newButton.image.SetAllDirty();
                DebugRandAddi.Log("Created new " + name);
            }
        }

        /*
        [HarmonyPatch(typeof(FmodGvrAudioRoom))]
        [HarmonyPatch("Update")]//
        internal static class EatEscapeKeypress
        {
            internal static void Prefix(FmodGvrAudioRoom __instance)
            {
                DebugRandAddi.Log("FmodGvrAudioRoom ACTIVE");
            }
        }*/

        [HarmonyPatch(typeof(ManRadar))]
        [HarmonyPatch("IconTypeCount", MethodType.Getter)]//
        internal static class ExtendRadarIconsCount
        {
            internal static bool Prefix(TooltipComponent __instance, ref int __result)
            {
                if (ManMinimapExt.AddedMinimapIndexes < ManMinimapExt.VanillaMapIconCount)
                    __result = ManMinimapExt.VanillaMapIconCount;
                else
                    __result = ManMinimapExt.AddedMinimapIndexes;
                //DebugRandAddi.Log("IconTypeCount returned " + __result);
                return false;
            }
        }
        [HarmonyPatch(typeof(TooltipComponent))]
        [HarmonyPatch("OnPointerEnter")]//
        internal static class CatchHoverInMapUI
        {
            internal static void Prefix(TooltipComponent __instance)
            {
                if (__instance?.gameObject && __instance.gameObject.GetComponent<UIMiniMapElement>())
                    ManMinimapExt.LastModaledTarget = __instance.gameObject.GetComponent<UIMiniMapElement>();
            }
        }
        [HarmonyPatch(typeof(TooltipComponent))]
        [HarmonyPatch("OnPointerExit")]//
        internal static class CatchHoverInMapUI2
        {
            internal static void Prefix(TooltipComponent __instance)
            {
                if (__instance?.gameObject && __instance.gameObject.GetComponent<UIMiniMapElement>())
                    if (ManMinimapExt.LastModaledTarget == __instance.gameObject.GetComponent<UIMiniMapElement>())
                        ManMinimapExt.LastModaledTarget = null;
            }
        }
        /*
        [HarmonyPatch(typeof(UIHUDWorldMap))]
        [HarmonyPatch("TryGetWaypoint")]//
        internal static class LaunchModalOnMap
        {
            internal static void Prefix(GameObject cursorGO)
            {
                if (cursorGO != null)
                {
                    UIMiniMapElement uiminiMapElement = cursorGO.GetComponent<UIMiniMapElement>();
                    if (uiminiMapElement?.TrackedVis != null && 
                        !(uiminiMapElement.TrackedVis.ObjectType == ObjectTypes.Waypoint || 
                        uiminiMapElement.TrackedVis.RadarType == RadarTypes.MapNavTarget))
                    {
                        ManMinimapExt.BringUpMinimapModal(uiminiMapElement);
                    }
                }
            }
        }*/
        [HarmonyPatch(typeof(EncounterDetails))]
        [HarmonyPatch("AmountToAwardFromPool", MethodType.Getter)]//
        internal static class AllowAdjustableLoot
        {
            private static void Postfix(ref int __result)
            {
                if (RandomWorld.inst.WorldAltered)
                    __result = Mathf.RoundToInt(__result * RandomWorld.inst.LootBlocksMulti);
            }
        }
        [HarmonyPatch(typeof(EncounterDetails))]
        [HarmonyPatch("BBAmount", MethodType.Getter)]//
        internal static class AllowAdjustableEarnings
        {
            private static void Postfix(ref int __result)
            {
                if (RandomWorld.inst.WorldAltered)
                    __result = Mathf.RoundToInt(__result * RandomWorld.inst.LootBBMulti);
            }
        }
        [HarmonyPatch(typeof(EncounterDetails))]
        [HarmonyPatch("XPAmount", MethodType.Getter)]//
        internal static class AllowAdjustableXp
        {
            private static void Postfix(ref int __result)
            {
                if (RandomWorld.inst.WorldAltered)
                __result = Mathf.RoundToInt(__result * RandomWorld.inst.LootXpMulti);
            }
        }
        [HarmonyPatch(typeof(TechAudio))]
        [HarmonyPatch("PlayOneshot", new Type[] { typeof(TechAudio.AudioTickData), typeof(FMODEvent.FMODParams)})]//
        internal static class PlaySoundProperly
        {
            private static bool Prefix(ref TechAudio.AudioTickData data, ref FMODEvent.FMODParams additionalParam)
            {
                if ((int)data.sfxType < 0)
                {
                    ManSFXExtRand.PlaySound(data);
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ManSFX))]
        [HarmonyPatch("TryStartProjectileSFX")]//
        internal static class StartProperly
        {
            private static bool Prefix(ref ManSFX.ProjectileFlightType sfxType, ref Transform transform)
            {
                if (sfxType < 0)
                {
                    ManSFXExtRand.PlaySound(sfxType, transform);
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ManSFX))]
        [HarmonyPatch("TryStopProjectileSFX", new Type[] { typeof(ManSFX.ProjectileFlightType), typeof(Transform) })]//
        internal static class StopProperly
        {
            private static bool Prefix(ref ManSFX.ProjectileFlightType sfxType, ref Transform transform)
            {
                if (sfxType < 0)
                {
                    ManSFXExtRand.StopSound(sfxType, transform);
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ManSFX))]
        [HarmonyPatch("PlayExplosionSFX")]//
        internal static class ExplodeProperly
        {
            private static bool Prefix(ref Vector3 position, ref ManSFX.ExplosionType type)
            {
                if (type < 0)
                {
                    ManSFXExtRand.PlaySound(type, position);
                    return false;
                }
                return true;
            }
        }


        /*
        [HarmonyPatch(typeof(StringLookup))]
        [HarmonyPatch("GetString")]//
        private class ShoehornText
        {
            private static bool Prefix(Localisation __instance, ref string bank, ref string id, ref string __result)
            {
                if (id == "MOD")
                {
                    __result = bank;
                    return false;
                }
                return true;
            }
        }*/

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



        // LEGACY - From  LocalCorpAudioExt, now merged!

        [HarmonyPatch(typeof(ManTechMaterialSwap))]
        [HarmonyPriority(9001)]
        [HarmonyPatch("GetMaterial")]//
        private static class TempPreventBlockCrashDueToNullMaterial
        {
            private static bool Prefix(ManTechMaterialSwap __instance, ref Material currentMaterial, ref Material __result)
            {
                if (currentMaterial == null)
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }

        private static int corpsVanilla = Enum.GetValues(typeof(FactionSubTypes)).Length;
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPriority(9001)]
        [HarmonyPatch("SetDanger", new Type[] { typeof(ManMusic.DangerContext), typeof(Tank), })]//
        private static class VeryyScary
        {
            private static readonly FieldInfo dangerFactor = typeof(ManMusic).GetField(
                "m_DangerHistory", BindingFlags.NonPublic | BindingFlags.Instance);
            internal static bool Prefix(ManMusic __instance, ref ManMusic.DangerContext context, ref Tank friendlyTech)
            {
                int corpIndex = (int)context.m_Corporation;
                if (corpIndex >= corpsVanilla)
                {
                    if (ManMusicEnginesExt.corps.TryGetValue(corpIndex, out CorpExtAudio CL))
                    {
                        if (ManMusicEnginesExt.isVanillaCorpDangerValid)
                        {
                            context.m_Corporation = CL.FallbackMusic;
                            ManMusicEnginesExt.SetDangerContextVanilla();
                            return true;
                        }
                        else
                        {
                            //Debug.Log("SetDanger - " + corpIndex + " playing...");
                            if (CL.combatMusicLoaded.Count > 0)
                            {
                                __instance.FadeDownAll();
                                //context.m_Corporation = FactionSubTypes.NULL;
                                ManMusicEnginesExt.SetDangerContext(CL, context.m_BlockCount, context.m_VisibleID);
                                return false;
                            }
                        }
                    }
                    if (ManMusicEnginesExt.isVanillaCorpDangerValid)
                    {
                        context.m_Corporation = FactionSubTypes.GSO;
                        ManMusicEnginesExt.SetDangerContextVanilla();
                        return true;
                    }
                    //ManMusic.inst.SetDangerMusicOverride(ManMusic.MiscDangerMusicType.None);
                    //context.m_Corporation = CL.FallbackMusic;
                    return !ManMusicEnginesExt.isModCorpDangerValid;
                }
                else
                {
                    ManMusicEnginesExt.SetDangerContextVanilla();
                    return !ManMusicEnginesExt.isModCorpDangerValid;
                }
            }
        }
        /*
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPatch("IsDangerous")]//
        private static class VeryyScary2
        {
            internal static void Postfix(ManMusic __instance, ref bool __result)
            {
                //if (__result)
                //    Debug.Log("Dangerous");
            }
        }*/
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPatch("SetMusicMixerVolume")]//
        private static class RedirectAudioControl
        {
            internal static bool Prefix(ManMusic __instance, ref float value)
            {
                ManMusicEnginesExt.currentMusicVol = value;
                if (ManMusicEnginesExt.isModCorpDangerValid)
                {
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(ManMusic))]
        [HarmonyPatch("GetMusicMixerVolume")]//
        private static class RedirectAudioControl2
        {
            internal static bool Prefix(ManMusic __instance, ref float __result)
            {
                if (ManMusicEnginesExt.isModCorpDangerValid)
                {
                    __result = ManMusicEnginesExt.currentMusicVol;
                    return false;
                }
                return true;
            }
        }



        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnSpawn")]//
        private static class MakeRev
        {
            private static void Prefix(Tank __instance)
            {
                TechExtAudio.Insure(__instance);
            }
        }
        [HarmonyPatch(typeof(TechAudio))]
        [HarmonyPatch("GetCorpParams")]//
        private static class RevRight
        {
            private static void Prefix(TechAudio __instance, ref TechAudio.UpdateAudioCache cache)
            {
                FactionSubTypes FST = __instance.Tech.GetMainCorp();
                int corpIndex = (int)FST;
                //Debug.Log("GetCorpParams - Maincorp " + corpIndex);
                if (corpIndex >= corpsVanilla)
                {
                    //Debug.Log("GetCorpParams - Maincorp modded");
                    if (ManMusicEnginesExt.corps.TryGetValue(corpIndex, out CorpExtAudio CL))
                    {
                        //Debug.Log("GetCorpParams - Maincorp has audio");
                        TechExtAudio.Insure(__instance.Tech);
                        if (!CL.hasEngineAudio)
                            cache.corpMain = CL.CorpEngine;
                    }
                    else
                        cache.corpMain = FactionSubTypes.GSO;
                }
            }
        }

        [HarmonyPatch(typeof(TechAudio))]
        [HarmonyPatch("GetSizeParam")]//
        private static class RevPitch
        {
            private static void Postfix(TechAudio __instance, ref float __result)
            {
                FactionSubTypes FST = __instance.Tech.GetMainCorp();
                int corpIndex = (int)FST;
                //Debug.Log("GetSizeParam - Maincorp " + corpIndex);
                if (ManMusicEnginesExt.corps.TryGetValue(corpIndex, out CorpExtAudio CL))
                {
                    __result *= CL.EnginePitchDeepMulti;
                    if (__result > CL.EnginePitchMax)
                        __result = CL.EnginePitchMax;
                }
            }
        }


    }
}
