using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using TerraTech.Network;
using TerraTechETCUtil;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    internal class GlobalPatches
    {
        // Note: add MonoBehaviourEvent<T> patches that disables it on creation, but enables it if there is subscribers and disables if none


        // BUGFIXES
        /*
        internal static class SeeIfPoolLaggy
        {
            internal static Type target = typeof(ComponentPool);


            // Turning off the repooler does indeed increase lag
            //[HarmonyPriority(-9001)]
            //internal static bool LateUpdate_Prefix(ComponentPool __instance)
            //{
            //    return false;
            //}
            

            public static Stopwatch watch = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void DepoolForMemory_Prefix(ComponentPool __instance)
            {
                watch.Restart();
            }
            [HarmonyPriority(-9001)]
            internal static void DepoolForMemory_Postfix(ComponentPool __instance)
            {
                watch.Stop();
                if (watch.ElapsedMilliseconds > 5)
                    DebugRandAddi.Log("ComponentPool.DepoolForMemory() took " +  watch.ElapsedMilliseconds + "ms on path " + StackTraceUtility.ExtractStackTrace());
            }


            public static Stopwatch watch2 = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void UpdatePoolHeap_Prefix(ComponentPool __instance)
            {
                watch2.Restart();
            }
            [HarmonyPriority(-9001)]
            internal static void UpdatePoolHeap_Postfix(ComponentPool __instance)
            {
                watch2.Stop();
                if (watch2.ElapsedMilliseconds > 5)
                    DebugRandAddi.Log("ComponentPool.UpdatePoolHeap() took " + watch2.ElapsedMilliseconds + "ms on path " + StackTraceUtility.ExtractStackTrace());
            }

            public static Stopwatch watch3 = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void LateUpdate_Prefix(ComponentPool __instance)
            {
                watch3.Restart();
            }
            [HarmonyPriority(-9001)]
            internal static void LateUpdate_Postfix(ComponentPool __instance)
            {
                watch3.Stop();
                if (watch.ElapsedMilliseconds > 5)
                    DebugRandAddi.Log("ComponentPool.LateUpdate() took " + watch.ElapsedMilliseconds + "ms on path " + StackTraceUtility.ExtractStackTrace());
            }

            public static Stopwatch watch4 = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void InterrogatePoolInterface_Prefix(ComponentPool __instance)
            {
                watch4.Restart();
            }
            [HarmonyPriority(-9001)]
            internal static void InterrogatePoolInterface_Postfix(ComponentPool __instance)
            {
                watch4.Stop();
                if (watch.ElapsedMilliseconds > 5)
                    DebugRandAddi.Log("ComponentPool.InterrogatePoolInterface() took " + watch.ElapsedMilliseconds + "ms on path " + StackTraceUtility.ExtractStackTrace());
            }

            public static Stopwatch watch5 = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void LookupPool_Prefix(ComponentPool __instance)
            {
                watch5.Restart();
            }
            [HarmonyPriority(-9001)]
            internal static void LookupPool_Postfix(ComponentPool __instance)
            {
                watch5.Stop();
                if (watch.ElapsedMilliseconds > 5)
                    DebugRandAddi.Log("ComponentPool.LookupPool() took " + watch.ElapsedMilliseconds + "ms on path " + StackTraceUtility.ExtractStackTrace());
            }
        }//*/

        /*
        internal static class ScrewYouEpicOnlineServices2
        {
            internal static Type target = typeof(LobbySystem);

            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static void CreateLobbySystem_Postfix(LobbySystem __result)
            {
                if (SKU.IsSteam && KickStart.IDontTrustEpicAtAll)
                {
                    if (__result is PlatformLobbySystem_EOS)
                    {
                        ManModGUI.ShowErrorPopup("Screw EOS, why is it starting on our Steam client when crossplay is disabled????");
                        throw new NullReferenceException("Screw EOS, why is it starting on our Steam client when crossplay is disabled????");
                    }
                }
            }
        }//*/
        internal static class ScrewYouEpicOnlineServices
        {
            internal static Type target = typeof(ManEOS);

            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static bool SetOfflineMode_Prefix(ManEOS __instance, bool isOffline)
            {
                if (KickStart.IDontTrustEpicAtAll)
                {
                    if (SKU.IsSteam)
                    {
                        if (!isOffline)
                        {
                            DebugRandAddi.Assert("RandomAdditions: ManEOS.SetOfflineMode was called!  You don't trust them so we deny the re-enable request");

                            return false;
                        }
                    }
                    if (SKU.IsEpicGS)
                        DebugRandAddi.Assert("RandomAdditions: IDontTrustEpicAtAll is true but we are on Epic Games Store!?! Bypass request ignored entirely.");
                    else
                        DebugRandAddi.Assert("RandomAdditions: IDontTrustEpicAtAll is true but we are on a non epic platform?");
                }
                return true;
            }
        }//*/


        private static void PopYouHaveTooManySavesInYourFolderWarning(ManGameMode.GameType type)
        {
            string theMessage = "TerraTech wasn't able to load the savefile correctly.\n" +
                "The issue is caused by something not sending any savedata over\n" +
                "when there are too many mods installed (boot > 1 minute)\n" +
                "or when there are too many saves for the game to load.\n\n" +
                "Check your ram usage and move any save files you aren't \n" +
                "using out of our saves folder to reduce the odds of this error.\n" +
                "It is also VERY possible if your ram is full and paging is disabled, \n" +
                "your computer will no longer be able load the save.\n\n";
            var target = ManSaveGame.GetCurrentUserGameSaveDir(type);
            if (Directory.Exists(target))
            {
                ManModGUI.ShowErrorPopup(theMessage +
                    "Press [Fix] to open your file system to your saves directory.", true,
                    () => {
                        if (Directory.Exists(target))
                            KickStart.OpenInExplorer(target);
                    });
            }
            else
            {
                ManModGUI.ShowErrorPopup(theMessage +
                    "For some reason the saves folder does not exist?", true);
            }
        }
        private static FieldInfo getTheTypeSaves = AccessTools.Field(typeof(UIScreenLoadSave), "m_GameTypeOfSaves");

        internal static class UIScreenLoadSavePatches
        {
            internal static Type target = typeof(UIScreenLoadSave);

            [HarmonyPriority(-9001)]
            internal static void OnSaveSelected_Prefix(UIScreenLoadSave __instance, UISave selectedSave)
            {
                if (selectedSave == null)
                {
                    DebugRandAddi.FatalError("selectedSave IS NULL");
                }
                else if (selectedSave.SaveInfo == null)
                {
                    DebugRandAddi.FatalError("selectedSave.SaveInfo IS NULL");
                }
                else if (!selectedSave.SaveInfo.CanLoadSave())
                {
                    DebugRandAddi.FatalError("selectedSave.SaveInfo cannot load save!!!");
                }
            }


            [HarmonyPriority(-9001)]
            internal static void SetSavedGameToLoad_Prefix(UIScreenLoadSave __instance,
                ToggleGroup ___m_ToggleGroup, UISave ___m_ActiveSave)
            {
                if (___m_ToggleGroup == null)
                {
                    DebugRandAddi.FatalError("m_ToggleGroup IS NULL");
                }
                else if (!___m_ToggleGroup.AnyTogglesOn())
                {   // This happens but it doesn't seem to negatively affect anything...
                    //DebugRandAddi.FatalError("m_ToggleGroup does not have any toggles selected!");
                    //DebugRandAddi.Assert("SwitchToMode call has null m_SwitchAction when it should have one!");
                }
                if (___m_ActiveSave == null)
                {
                    PopYouHaveTooManySavesInYourFolderWarning((ManGameMode.GameType)getTheTypeSaves.GetValue(__instance));
                    DebugRandAddi.FatalError("m_ActiveSave IS NULL");
                    //DebugRandAddi.Assert("SwitchToMode call has null m_SwitchAction when it should have one!");
                }
            }
        }
        internal static class ManGameModePatches
        {
            internal static Type target = typeof(ManGameMode);

            [HarmonyPriority(-9001)]
            internal static void SetupModeSwitchAction_Postfix(ManGameMode __instance, 
                ManGameMode.ModeSettings modeSettings, ManGameMode.GameType modeToSet, 
                string miscSubMode = null)
            {
                if (__instance == null)
                {
                    DebugRandAddi.FatalError("ManGameMode IS NULL");
                }
                else if (modeSettings == null)
                {
                    DebugRandAddi.FatalError("ManGameMode.SetupModeSwitchAction() resulted in " +
                        "NULL modeSettings!?! - Target mode is [" + modeToSet.ToString() + 
                        "], miscSubMode [" + miscSubMode +"]");
                }
                else if (modeSettings.m_SwitchAction == null)
                {
                    DebugRandAddi.FatalError("ManGameMode.SetupModeSwitchAction() resulted in " +
                        "NULL modeSettings.m_SwitchAction and target mode is [" + modeToSet.ToString() +
                        "], miscSubMode [" + miscSubMode + "]");
                }
            }
        }
        //*
        internal static class ModeSettingsPatches
        {
            internal static Type target = typeof(ManGameMode.ModeSettings);
            internal static FieldInfo field = typeof(ManGameMode.ModeSettings).GetField("m_nextModeCachedSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);

            /// <summary>
            /// SANITY CHECK
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static void SwitchToMode_Prefix(ManGameMode.ModeSettings __instance)
            {
                if (__instance == null)
                {
                    DebugRandAddi.FatalError("SwitchToMode call was called on a NULL ModeSettings!");
                }
                else
                {
                    if (__instance.m_SwitchAction == null)
                    {
                        DebugRandAddi.FatalError("SwitchToMode call has null m_SwitchAction when it should have one!");
                        //DebugRandAddi.Assert("SwitchToMode call has null m_SwitchAction when it should have one!");
                    }
                    if (field.GetValue(__instance) == null)
                    {
                        DebugRandAddi.FatalError("SwitchToMode call has null m_nextModeCachedSettings when it should have one!");
                    }
                }
            }

            /// <summary>
            /// Fix the bloody settings issue!
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static void SwitchToMode_Postfix(ManGameMode.ModeSettings __instance)
            {
                if (__instance == null)
                {
                    DebugRandAddi.Assert("__instance IS NULL");
                    return;
                }
                try
                {
                    string nextMode = Singleton.Manager<ManGameMode>.inst.GetModeInitSetting("ModeName") as string;
                    try
                    {
                        if (ModeMisc.inst.m_ModeSettings == null)
                        {
                            DebugRandAddi.Assert("No mode settings");
                        }
                        else
                        {
                            foreach (ModeMisc.ModeSpec modeSpec in ModeMisc.inst.m_ModeSettings)
                            {
                                if (modeSpec?.name != null && modeSpec.name.EqualsNoCase(nextMode) &&
                                    modeSpec.m_MyType == ManGameMode.GameType.RaD)
                                {
                                    if (__instance.GetModeInitSetting("BuildSizeLimit", out object data) && data is int)
                                    {
                                        EmergPatches.TrySaveAndLockSizeLimit();
                                    }
                                    else if (!EmergPatches.HasSizeLimitSet())
                                    {   // WE FORCE THE SIZE SETTINGS
                                        //__instance.AddModeInitSetting("BuildSizeLimit", 256);
                                        //Singleton.Manager<ManGameMode>.inst.AddModeInitSetting("BuildSizeLimit", 256);
                                        DebugRandAddi.Assert("Our R&D mode had NO BuildSizeLimit SET!!!");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e2)
                    {
                        DebugRandAddi.LogError("Failed to get mode settings for mode, skipping R&D check - " + e2);
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.LogError("Failed to get any settings for mode, skipping R&D check - " + e);
                }
            }

        }//*/


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
            [HarmonyPriority(-9001)]
            internal static void OnPool_Postfix(TankBlock __instance)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched TankBlock OnPool(ModuleOHKOInsurance & TankBlockScaler)");
                var block = __instance.gameObject;
                if (!(bool)block.GetComponent<TankBlockScaler>())
                {   //This allows for an override to be concocted if the block maker wants to specify a custom size
                    var setComp = block.AddComponent<TankBlockScaler>();
                    var bound = __instance.BlockCellBounds.extents;
                    setComp.AimedDownscale = Mathf.Min(Mathf.Max(0.001f, 1 / Mathf.Max(Mathf.Max(bound.x, bound.y), bound.z)) / 2, 0.5f);
                }
                var run = block.GetComponent<TankBlockScaler>();
                run.OnPool(__instance);
                run.enabled = false;

                var rp = block.GetComponent<ModuleReplace>();
                if ((bool)rp)
                    rp.Init(__instance);
            }
            /*
            internal static void OnAttach_Postfix(TankBlock __instance)
            {
                foreach (var item in ModulePatches.bubbleShields.Where(x => x?.RepulsorBulletTrigger != null))
                    __instance.IgnoreCollision(item.RepulsorBulletTrigger, true);
            }//*/

            /// <summary>
            /// PatchAllBlocksForPainting
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static void OnSpawn_Postfix(TankBlock __instance)
            {
                try
                {
                    if (__instance?.transform != null)
                        ModdedBlockFixes.FixBlock(__instance);
#if DEBUG
                    if (KickStart.OcculsionCullingInit && !__instance.GetComponent<GraphicsPhysicsCulling.BlockRenderBounds>())
                        GraphicsPhysicsCulling.BlockRenderBounds.PoolInit(__instance);
#endif
                }
                catch (Exception e)
                {
                    DebugRandAddi.FatalError("Failed to apply ModdedBlockFixes.FixBlock() for \"" + 
                        (__instance.name.NullOrEmpty() ? "<NULL>" : __instance.name) + "\" - " + e);
                }
            }

            /*
            /// <summary>
            /// TrySeeIfThisIsLaggy
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
            internal static bool ForeachConnectedBlock_Prefix(TankBlock __instance)
            {
                return false;
            }*/
        }

        internal static class VisiblePatches
        {
            internal static Type target = typeof(Visible);
            //Allow rescale of blocks on grab
            /// <summary>
            /// PatchVisibleForBlocksRescaleOnConveyor
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static void SetHolder_Prefix(Visible __instance, ref ModuleItemHolder.Stack stack)
            {
                if ((bool)__instance.block)
                {
                    if (!__instance.block.IsAttached)
                    {
                        //DebugRandAddi.Log("RandomAdditions: Overwrote visible to handle resources");
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
                                ModuleScale.Rescale(true);
                                //DebugRandAddi.Log("RandomAdditions: Queued Rescale Down");
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
                            ModuleScale.Rescale(false);
                            //DebugRandAddi.Log("RandomAdditions: Queued Rescale Up");

                            //Reset them collodos
                            if (__instance.ColliderSwapper.CollisionEnabled)
                            {   //BUT NO TOUCH THE DISABLED ONES
                                __instance.ColliderSwapper.EnableCollision(false);
                                __instance.ColliderSwapper.EnableCollision(true);
                            }
                            //DebugRandAddi.Log("RandomAdditions: reset " + __instance.name + "'s active colliders");

                        }
                    }
                }
                if ((bool)__instance.pickup)
                {
                    //DebugRandAddi.Log("RandomAdditions: Overwrote visible to handle resources");
                    if (stack != null)
                    {

                        var ModuleCheck = stack.myHolder.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                        if (ModuleCheck != null)
                        {
                            if (ModuleCheck.FixateToTech)
                            {
                                ItemIgnoreCollision.Insure(__instance).UpdateCollision(true);
                            }
                            if (ModuleCheck.AllowOtherTankCollision)
                            {
                                ItemIgnoreCollision.Insure(__instance).AllowOtherTankCollisions = true;
                            }
                        }
                    }
                    else
                    {
                        ItemIgnoreCollision.Insure(__instance).UpdateCollision(false);
                    }
                }
            }

        }
        internal static class ObjectSpawnerPatches
        {
            internal static Type target = typeof(ObjectSpawner);

            [HarmonyPriority(-9001)]
            internal static void TrySpawn_Prefix(ref ManSpawn.ObjectSpawnParams objectSpawnParams, ref ManFreeSpace.FreeSpaceParams freeSpaceParams)
            {
                if ((ManNetwork.IsHost || !ManNetwork.IsNetworked) && objectSpawnParams != null)
                {
                    if (objectSpawnParams is ManSpawn.TechSpawnParams TSP)
                    {
                        if (TSP.m_IsPopulation)
                        {
                            ReplaceManager.TryReplaceBlocks(TSP.m_TechToSpawn, freeSpaceParams);
                        }
                    }
                }
            }
        }

        /*
        internal static class ResourcePickupPatches
        {
            internal static Type target = typeof(ResourcePickup);
            /// <summary>
            /// PatchAllChunks
            /// </summary>
            /// <param name="__instance"></param>
            private static void OnPool_Postfix(ResourcePickup __instance)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched ResourcePickup OnPool(ModulePickupIgnore)");
                var chunk = __instance.gameObject;
                chunk.AddComponent<ItemIgnoreCollision>();
            }
        }*/

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
            [HarmonyPriority(-9001)]
            internal static void Damage_Prefix(Damageable __instance, ref ManDamage.DamageInfo info)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Damageable Damage(ModuleReinforced)");
                if (__instance == null)
                    return;
                var multi = __instance.GetComponent<ModuleReinforced>();
                if (multi != null)
                    info = multi.RecalcDamage(ref info);
            }

            /// <summary>
            /// PatchDamageableChange
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static void OnPool_Prefix(Damageable __instance)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Damageable OnPool(ModuleReinforced)");
                var modifPresent = __instance.gameObject.GetComponent<ModuleReinforced>();
                if (modifPresent != null)
                {
                    if (modifPresent.DoDamagableSwitch)
                    {
                        __instance.DamageableType = modifPresent.TypeToSwitch;
                        //DebugRandAddi.Log("RandomAdditions: Damageable Switched to " + __instance.DamageableType);
                    }
                    else if (modifPresent.UseMultipliers)
                        __instance.DamageableType = ManDamage.DamageableType.Standard;
                }
            }

        }
        internal static class ModuleDamagePatches
        {
            internal static Type target = typeof(ModuleDamage);
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            // Used for Custom Blocks

            // Allow blocks to have one special resistance
            /// <summary>
            /// PatchDamageableRA
            /// </summary>
            [HarmonyPriority(-9001)]
            internal static bool OnDamaged_Prefix(ModuleDamage __instance, ref ManDamage.DamageInfo info)
            {
                if (__instance.GetComponent<ModuleReinforced>() != null)
                    return info.Damage > 0;
                return true;
            }
        }
        internal static class BubbleShieldPatches
        {
            internal static Type target = typeof(BubbleShield);
            /// <summary>
            /// PatchShieldsToActuallyBeShieldTyping
            /// </summary>
            /// <param name="__instance"></param>
            internal static void OnSpawn_Postfix(BubbleShield __instance)
            {
                try
                {
                    if (KickStart.TrueShields && __instance.Damageable.DamageableType == ManDamage.DamageableType.Standard)
                    {
                        __instance.Damageable.DamageableType = ManDamage.DamageableType.Shield;
                        //DebugRandAddi.Log("RandomAdditions: PatchShieldsToActuallyBeShieldTyping - Changed " + __instance.transform.root.name + " to actually be shield typing");
                    }
                }
                catch (Exception e)
                {
                    DebugRandAddi.Log("RandomAdditions: PatchShieldsToActuallyBeShieldTyping - Error on " + __instance.transform.root.name + e);
                }
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
        internal static class TechAudioPatches
        {
            internal static Type target = typeof(TechAudio);

            [HarmonyPriority(-9001)]
            internal static bool OnModuleTickData_Prefix(ref TechAudio.AudioTickData tickData, ref FMODEvent.FMODParams additionalParam)
            {
                if ((int)tickData.sfxType < 0)
                {
                    ManSFXExtRand.PlaySound(tickData);
                    return false;
                }
                return true;
            }
        }
        internal static class ManModsPatches
        {
            internal static Type target = typeof(ManMods);

            [HarmonyPriority(-9001)]
            internal static void RequestReloadAllMods_Postfix(ManMods __instance)
            {
                KickStart.didQuickstart = false;
            }

            /// <summary>
            /// Insure we load SFX of corps properly!
            /// </summary>
            /// <param name="__instance"></param>

            [HarmonyPriority(-91)]
            internal static void InjectModdedCorps_Postfix(ManMods __instance)
            {
                ManMusicEnginesExt.inst.RefreshModCorpAudio();
            }

            /// <summary>
            /// Inject in the new custom recipes
            /// </summary>
            [HarmonyPriority(-90001)]
            internal static void InjectModdedBlocks_Prefix(ManMods __instance)
            { 

            }

            /*
            internal static Stopwatch TimeToLoadAllBlocks = new Stopwatch();
            [HarmonyPriority(-9001)]
            internal static void InjectModdedBlocks_Prefix(ManMods __instance)
            {
                if (DebugRandAddi.ShouldLog)
                {
                    TimeToLoadAllBlocks.Reset();
                    DebugRandAddi.Log("Started loading blocks");
                    TimeToLoadAllBlocks.Start();
                }
            }
            [HarmonyPriority(-9001)]
            internal static void InjectModdedBlocks_Postfix(ManMods __instance)
            {
                if (DebugRandAddi.ShouldLog)
                {
                    TimeToLoadAllBlocks.Stop();
                    DebugRandAddi.Log("Finished loading all blocks.  Took " + TimeToLoadAllBlocks.ElapsedMilliseconds + " Milisecond(s)");
                }
            }
            */
        }
    }
}
