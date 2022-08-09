﻿using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace RandomAdditions
{
    internal class GlobalPatches : MassPatcher
    {
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // The NEW crash handler with useful mod-crash-related information

        internal static class ModePatches
        {
            internal static Type target = typeof(Mode);
#if !STEAM
            /// <summary>
            /// Startup
            /// </summary>
            private static void EnterPreMode_Prefix()
            {
                KickStart.DelayedInitAll();
            }
#endif
        }

        internal static class UIScreenBugReportPatches
        {
            internal static Type target = typeof(UIScreenBugReport);
            private static FieldInfo cat = typeof(UIScreenBugReport).GetField("m_ErrorCatcher", BindingFlags.NonPublic | BindingFlags.Instance);
            /// <summary>
            /// AllowBypass
            /// IMPORTANT
            /// </summary>
            private static void Show_Prefix(UIScreenBugReport __instance)
            {   //Custom error menu
#if STEAM
                DebugRandAddi.Log("RandomAdditions: Letting the player continue with a crashed STEAM client. " +
                    "Note that this will still force a quit screen under certain conditions.");
#else
                Debug.Log("RandomAdditions: Letting the player continue with a crashed Unofficial client. " +
                "Note that this will NOT force a quit screen under certain conditions, but " +
                "you know the rules, and so do I.");
#endif
                if (!ManNetwork.IsNetworked)
                    cat.SetValue(__instance, false);
            }

            /// <summary>
            /// DisableCrashTextMenu
            /// DO NEXT OF ABOVE
            /// </summary>
            private static bool PostIt_Prefix(UIScreenBugReport __instance)
            {
                //Custom error menu
                DebugRandAddi.Log("RandomAdditions: DISABLED POSTIT");
                return false; //end it before it can display the text field
            }

            /// <summary>
            /// OverhaulCrashPatch
            /// DO NEXT OF ABOVE
            /// </summary>
            private static void Set_Postfix(UIScreenBugReport __instance)
            {
                //Custom error menu
                //  Credit to Exund who provided the nesseary tools to get this working nicely!
                //Debug.Log("RandomAdditions: Fired error menu"); 
                FieldInfo errorGet = typeof(UIScreenBugReport).GetField("m_Description", BindingFlags.NonPublic | BindingFlags.Instance);
                var UIMan = Singleton.Manager<ManUI>.inst;
                var UIObj = UIMan.GetScreen(ManUI.ScreenType.BugReport).gameObject;
                Text bugReport = (Text)errorGet.GetValue(__instance);
                //UIObj.GetComponent<UIScreenBugReport>()

                //ETC
                //Text newError = (string)textGet.GetValue(bugReport);
                //Debug.Log("RandomAdditions: error menu " + bugReport.text.ToString());
                //Debug.Log("RandomAdditions: error menu host gameobject " +
                //Nuterra.NativeOptions.UIUtilities.GetComponentTree(UIObj, ""));

                //Cleanup of unused UI elements
                var reportBox = UIObj.transform.Find("ReportLayout").Find("Panel");
                reportBox.Find("Description").gameObject.SetActive(false);
                reportBox.Find("Submit").gameObject.SetActive(false);
                reportBox.Find("Email").gameObject.SetActive(false);
                //reportBox.Find("Description").gameObject.GetComponent<InputField>().

                //reportBox.Find(" Title").GetComponent<Text>().text = "<b>BUG REPORTER [MODDED!]</b>";

#if STEAM
                UIObj.transform.Find("ReportLayout").Find("Button Forward").Find("Text").GetComponent<Text>().text = "(CORRUPTION WARNING) Ignore & Continue";
#else
                UIObj.transform.Find("ReportLayout").Find("Button Forward").Find("Text").GetComponent<Text>().text = "(CORRUPTION WARNING) CONTINUE ANYWAYS";
#endif
                //Debug.Log("RandomAdditions: Cleaned Bug Reporter UI");

                //Setup the UI
                StringBuilder SB = new StringBuilder();
                string toSearch = Application.consoleLogPath;
                bool ignoreThisCase = true;

                int stoppingPos = toSearch.IndexOf("Users") + 6;
                for (int step = 0; step < toSearch.Length; step++)
                {
                    if (stoppingPos <= step)
                    {
                        if (stoppingPos == step)
                        {
                            SB.Append("userName");
                        }
                        //Debug.Log("RandomAdditions: " + toSearch[step] + " | " );
                        if (toSearch[step] == '/')
                            ignoreThisCase = false;
                        if (ignoreThisCase)
                            continue;
                    }
                    SB.Append(toSearch[step]);
                }
                string outputLogLocation = SB.ToString(); //"(Error on OS fetch request)";


                try
                {   //<color=#f23d3dff></color> - tried that but it's too hard to read
                    string latestError = KickStart.logMan.GetComponent<LogHandler>().GetErrors();
#if STEAM
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n<b>This is a modded game which the developers cannot manage!</b>  " +
                        "\nTake note of all your official mods and send the attached Bug Report (make sure your name isn't in it!) below in the Official TerraTech Discord, in #modding-official.";
#else
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b>  " +
                        "\n<b>Make sure your name isn't in the Report below first,</b> then take note of all your mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial.";
#endif

                    //var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Description"), UIObj.transform, false);
                    var errorList = reportBox.Find("Description");
                    //Vector3 offset = errorList.localPosition;
                    //offset.y -= 340;
                    //errorList.localPosition = offset;
                    var rect = errorList.GetComponent<RectTransform>();
                    var pos = rect.transform;
                    pos.Translate(new Vector3(0, -0.25f, 0));
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 325);
                    errorList.gameObject.SetActive(true);
                    //var errorText = errorList.gameObject.GetComponent<Text>();
                    var errorField = errorList.gameObject.GetComponent<InputField>();
                    errorField.text = "-----  TerraTech [Modded] Automatic Crash Report  -----\n  The log file is at: " + outputLogLocation + "\n<b>Error:</b> " + latestError;
                    var errorText = errorField.textComponent;
                    //errorText.alignByGeometry = true;
                    errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.resizeTextMinSize = 16;
                    //errorText.fontSize = 16;
                    errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    //errorText.verticalOverflow = VerticalWrapMode.Overflow;
                    //bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<color=#f23d3dff>Please DON'T press</color> <b>SEND</b>!  <color=#f23d3dff>This is a MODDED GAME AND THAT DOESN'T WORK!</color>  \nYou can skip this screen with the button at the upper-left corner and continue in your world, but do remember you are putting your save at risk! \n\nError: ";
                }
                catch
                {
                    DebugRandAddi.Log("RandomAdditions: FAILIURE ON FETCHING LOG!");
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b> \nTake note of all your unofficial mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial. \n\nThe log file is at: " + outputLogLocation;
                    var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Explanation"), UIObj.transform, false);
                    Vector3 offset = errorList.localPosition;
                    offset.y -= 340;
                    errorList.localPosition = offset;
                    var errorText = errorList.gameObject.GetComponent<Text>();
                    //errorText.alignByGeometry = true;
                    //errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.fontSize = 50;
                    errorText.text = "<b>COULD NOT FETCH ERROR!!!</b>";
                }
            }

        }

        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // Game tweaks
        internal static class TankBlockPatches
        {
            internal static Type target = typeof(TankBlock);
            /// <summary>
            /// PatchAllBlocksForOHKOProjectile
            /// </summary>
            /// <param name="__instance"></param>
            internal static void OnPool_Postfix(TankBlock __instance)
            {
                //Debug.Log("RandomAdditions: Patched TankBlock OnPool(ModuleOHKOInsurance & TankBlockScaler)");
                var block = __instance.gameObject;
                if (!(bool)block.GetComponent<TankBlockScaler>())
                {   //This allows for an override to be concocted if the block maker wants to specify a custom size
                    var setComp = block.AddComponent<TankBlockScaler>();
                    var bound = __instance.BlockCellBounds.extents;
                    setComp.AimedDownscale = Mathf.Min(Mathf.Max(0.001f, 1 / Mathf.Max(Mathf.Max(bound.x, bound.y), bound.z)) / 2, 0.5f);
                }
                var run = block.GetComponent<TankBlockScaler>();
                run.OnPool();
                run.enabled = false;

                var rp = block.GetComponent<ModuleReplace>();
                if ((bool)rp)
                    rp.Init(__instance);
            }

            /// <summary>
            /// PatchAllBlocksForPainting
            /// </summary>
            /// <param name="__instance"></param>
            internal static void OnSpawn_Postfix(TankBlock __instance)
            {
                OptimizeOutline.FlagNonRendTrans(__instance.transform);
            }
        }

        internal static class VisiblePatches
        {
            internal static Type target = typeof(Visible);
            //Allow rescale of blocks on grab
            /// <summary>
            /// PatchVisibleForBlocksRescaleOnConveyor
            /// </summary>
            internal static void SetHolder_Prefix(Visible __instance, ref ModuleItemHolder.Stack stack)
            {
                if ((bool)__instance.block)
                {
                    if (!__instance.block.IsAttached)
                    {
                        //Debug.Log("RandomAdditions: Overwrote visible to handle resources");
                        if (stack != null)
                        {
                            if (KickStart.AutoScaleBlocksInSCU || !stack.myHolder.gameObject.GetComponent<ModuleHeart>())
                            {
                                var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
                                if (!ModuleScale)
                                {   //This allows for an override to be concocted if the block maker wants to specify a custom size
                                    ModuleScale = __instance.gameObject.AddComponent<TankBlockScaler>();
                                    var bound = __instance.block.BlockCellBounds.extents;
                                    ModuleScale.AimedDownscale = Mathf.Min(Mathf.Max(0.001f, 1 / Mathf.Max(Mathf.Max(bound.x, bound.y), bound.z)) / 2, 0.5f);
                                }
                                ModuleScale.Downscale = true;
                                ModuleScale.enabled = true;
                                //Debug.Log("RandomAdditions: Queued Rescale Down");
                            }

                            var ModuleCheck = stack.myHolder.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                            if (ModuleCheck != null)
                            {
                                int firedCount = 0;
                                //item.gameObject.transform.parent = __instance.transform.root;
                                GameObject stackTank = stack.myHolder.transform.root.gameObject;
                                foreach (Collider collTonk in stackTank.GetComponentsInChildren<Collider>())
                                {
                                    foreach (Collider collVis in __instance.gameObject.GetComponentsInChildren<Collider>())
                                    {
                                        if (collVis.enabled && collVis.gameObject.layer != Globals.inst.layerShieldBulletsFilter)
                                        {
                                            Physics.IgnoreCollision(collTonk, collVis);
                                            firedCount++;
                                        }
                                    }
                                }
                                DebugRandAddi.Info("RandomAdditions: made " + __instance.name + " ignore " + firedCount + " colliders.");
                            }
                        }
                        else
                        {
                            var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
                            if (!ModuleScale)
                            {   //This allows for an override to be concocted if the block maker wants to specify a custom size
                                ModuleScale = __instance.gameObject.AddComponent<TankBlockScaler>();
                                var bound = __instance.block.BlockCellBounds.extents;
                                ModuleScale.AimedDownscale = Mathf.Min(Mathf.Max(0.001f, 1 / Mathf.Max(Mathf.Max(bound.x, bound.y), bound.z)) / 2, 0.5f);
                            }
                            ModuleScale.Downscale = false;
                            ModuleScale.enabled = true;
                            //Debug.Log("RandomAdditions: Queued Rescale Up");

                            //Reset them collodos
                            if (__instance.ColliderSwapper.CollisionEnabled)
                            {   //BUT NO TOUCH THE DISABLED ONES
                                __instance.ColliderSwapper.EnableCollision(false);
                                __instance.ColliderSwapper.EnableCollision(true);
                            }
                            //Debug.Log("RandomAdditions: reset " + __instance.name + "'s active colliders");

                        }
                    }
                }
                if ((bool)__instance.pickup)
                {
                    //Debug.Log("RandomAdditions: Overwrote visible to handle resources");
                    if (stack != null)
                    {

                        var ModuleCheck = stack.myHolder.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                        if (ModuleCheck != null)
                        {
                            if (ModuleCheck.FixateToTech)
                            {
                                var IIC = __instance.gameObject.GetComponent<ItemIgnoreCollision>();
                                if (!IIC)
                                {
                                    IIC = __instance.gameObject.AddComponent<ItemIgnoreCollision>();
                                }
                                IIC.UpdateCollision(true);
                            }
                            if (ModuleCheck.AllowOtherTankCollision)
                            {
                                var IIC = __instance.gameObject.GetComponent<ItemIgnoreCollision>();
                                if (!IIC)
                                {
                                    IIC = __instance.gameObject.AddComponent<ItemIgnoreCollision>();
                                }
                                IIC.AllowOtherTankCollisions = true;
                            }
                        }
                    }
                    else
                    {
                        var IIC = __instance.gameObject.GetComponent<ItemIgnoreCollision>();
                        if (!IIC)
                        {
                            IIC = __instance.gameObject.AddComponent<ItemIgnoreCollision>();
                        }
                        IIC.UpdateCollision(false);
                    }
                }
            }

        }
        internal static class TrackedVisiblePatches
        {
            internal static Type target = typeof(TrackedVisible);
            private static bool TankExists(TrackedVisible tv)
            {
                if (tv != null)
                {
                    if (tv.visible != null)
                    {
                        if (ManWorld.inst.CheckIsTileAtPositionLoaded(tv.Position))
                        {
                            if (tv.visible.tank != null)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }

            /// <summary>
            /// PerformBlocksSwapOperation
            /// </summary>
            /// 
            private static void OnRespawn_Postfix(TrackedVisible __instance)
            {   // Change the parts on the Tech with blocks with ModuleReplace attached
                if (!TankExists(__instance))
                    return;
                Tank tank = __instance.visible.tank;
                ReplaceManager.TryReplaceBlocks(tank);
            }
        }

        internal static class ResourcePickupPatches
        {
            internal static Type target = typeof(ResourcePickup);
            /// <summary>
            /// PatchAllChunks
            /// </summary>
            /// <param name="__instance"></param>
            private static void OnPool_Postfix(ResourcePickup __instance)
            {
                //Debug.Log("RandomAdditions: Patched ResourcePickup OnPool(ModulePickupIgnore)");
                var chunk = __instance.gameObject;
                chunk.AddComponent<ItemIgnoreCollision>();
            }
        }

        internal static class DamageablePatches
        {
            internal static Type target = typeof(Damageable);
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            // Both used for Custom Blocks and Projectiles

            // Allow blocks to have one special resistance
            /// <summary>
            /// PatchDamageableRA
            /// </summary>
            private static void Damage_Prefix(Damageable __instance, ref ManDamage.DamageInfo info)
            {
                //Debug.Log("RandomAdditions: Patched Damageable Damage(ModuleReinforced)");
                try
                {
                    if (__instance == null)
                        return;
                    var multi = __instance.GetComponent<ModuleReinforced>();
                    if (multi != null)
                    {
                        info = multi.RecalcDamage(ref info);
                    }
                }
                catch { }
            }

            /// <summary>
            /// PatchDamageableChange
            /// </summary>
            private static void OnPool_Prefix(Damageable __instance)
            {
                //Debug.Log("RandomAdditions: Patched Damageable OnPool(ModuleReinforced)");
                var modifPresent = __instance.gameObject.GetComponent<ModuleReinforced>();
                if (modifPresent != null)
                {
                    if (modifPresent.DoDamagableSwitch)
                    {
                        __instance.DamageableType = modifPresent.TypeToSwitch;
                        //Debug.Log("RandomAdditions: Damageable Switched to " + __instance.DamageableType);
                    }
                    else if (modifPresent.UseMultipliers)
                    {
                        __instance.DamageableType = ManDamage.DamageableType.Standard;
                    }
                }
            }

        }
        internal static class BubbleShieldPatches
        {
            internal static Type target = typeof(BubbleShield);
            /// <summary>
            /// PatchShieldsToActuallyBeShieldTyping
            /// </summary>
            /// <param name="__instance"></param>
            private static void OnSpawn_Postfix(BubbleShield __instance)
            {
                try
                {
                    if (KickStart.TrueShields && __instance.Damageable.DamageableType == ManDamage.DamageableType.Standard)
                    {
                        __instance.Damageable.DamageableType = ManDamage.DamageableType.Shield;
                        //Debug.Log("RandomAdditions: PatchShieldsToActuallyBeShieldTyping - Changed " + __instance.transform.root.name + " to actually be shield typing");
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: PatchShieldsToActuallyBeShieldTyping - Error on " + __instance.transform.root.name + e);
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
            private static bool SetCameraShake_Prefix(TankCamera __instance)
            {
                if (KickStart.NoShake)
                {
                    //Debug.Log("RandomAdditions: Stopping irritation.");
                    shaker.SetValue(__instance, 0);
                    return false;
                }
                return true;
            }
        }
        internal static class PlayerFreeCameraPatches
        {
            internal static Type target = typeof(PlayerFreeCamera);
            const float newMaxFreeCamRange = 95000;// 250
            const float newMPMaxFreeCamRange = 250;

            /// <summary>
            /// MORE_RANGE
            /// </summary>
            static FieldInfo overr = typeof(PlayerFreeCamera).GetField("maxDistanceFromTank", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo underr = typeof(PlayerFreeCamera).GetField("groundClearance", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Enable_Prefix(PlayerFreeCamera __instance)
            {
                if (ManNetwork.IsNetworked)
                    overr.SetValue(__instance, newMPMaxFreeCamRange);    // max "safe" range - 2.5x vanilla
                else
                    overr.SetValue(__instance, newMaxFreeCamRange);    // max "safe" range - You are unlikely to hit the limit
                underr.SetValue(__instance, -25f);   // cool fights underground - You can do this in Build Beam so it makes sense
                DebugRandAddi.Log("RandomAdditions: EXTENDED Free cam range");
            }

        }
        /*
        static MethodInfo audo = typeof(TechAudio).GetMethod("PlayOneShotClamped", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(TechAudio))]
        [HarmonyPatch("PlayOneshot")]
        private class InsertAudio1
        {
            private static bool Prefix(TechAudio __instance, ref TechAudio.AudioTickData data, ref FMODEvent.FMODParams additionalParam)
            {
                var sound = data.module.GetComponent<ModuleSoundOverride>();
                if (sound)
                {
                    audo.Invoke(__instance, new object[] { data.sfxType, sound.sound, data.sfxType})
                    return false;
                }
                return true;
            }
        }*/
        internal static class TileManagerPatches
        {
            internal static Type target = typeof(TileManager);
            private static List<IntVector2> starTiles = null;
            private static Dictionary<IntVector2, WorldTile> exisTiles = null;
            static FieldInfo TilesNew = typeof(TileManager).GetField("m_TileCoordsToCreateWorking", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo Tiles = typeof(TileManager).GetField("m_TileLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            /// <summary>
            /// EnableTileLoading
            /// </summary>
            private static void UpdateTileRequestStatesInStandardMode_Postfix(TileManager __instance)
            {
                if (starTiles == null)
                    starTiles = (List<IntVector2>)TilesNew.GetValue(__instance);
                if (exisTiles == null)
                    exisTiles = (Dictionary<IntVector2, WorldTile>)Tiles.GetValue(__instance);

                foreach (var item in ManTileLoader.RequestedLoaded)
                {
                    if (exisTiles.ContainsKey(item))
                    {
                        if (exisTiles.TryGetValue(item, out WorldTile WT))
                        {
                            WT.m_RequestState = WorldTile.State.Loaded;
                        }
                    }
                    else
                    {
                        if (!starTiles.Contains(item))
                            starTiles.Add(item);
                    }
                }
            }
        }



    }
}
