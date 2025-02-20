using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using TerraTechETCUtil;
using RandomAdditions.Minimap;
using HarmonyLib;
using FMOD;

namespace RandomAdditions
{
    internal class GlobalPatches
    {

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
                run.OnPool();
                run.enabled = false;

                var rp = block.GetComponent<ModuleReplace>();
                if ((bool)rp)
                    rp.Init(__instance);
                InvokeHelper.Invoke(OnPool_Delayed, 3, __instance);
            }
            [HarmonyPriority(-9001)]
            internal static void OnPool_Delayed(TankBlock block)
            {
                new WikiPageBlock((int)block.GetComponent<Visible>().ItemType);
            }

            /// <summary>
            /// PatchAllBlocksForPainting
            /// </summary>
            /// <param name="__instance"></param>
            [HarmonyPriority(-9001)]
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


        internal static class ObjectSpawnerPatches
        {
            internal static Type target = typeof(ObjectSpawner);

            [HarmonyPriority(-9001)]
            private static void TrySpawn_Prefix(ref ManSpawn.ObjectSpawnParams objectSpawnParams, ref ManFreeSpace.FreeSpaceParams freeSpaceParams)
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
            private static bool Damage_Prefix(Damageable __instance, ref ManDamage.DamageInfo info)
            {
                //DebugRandAddi.Log("RandomAdditions: Patched Damageable Damage(ModuleReinforced)");
                if (__instance == null)
                    return true;
                var multi = __instance.GetComponent<ModuleReinforced>();
                if (multi != null)
                {
                    info = multi.RecalcDamage(ref info);
                }
                return true;
            }

            /// <summary>
            /// PatchDamageableChange
            /// </summary>
            [HarmonyPriority(-9001)]
            private static void OnPool_Prefix(Damageable __instance)
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
                    {
                        __instance.DamageableType = ManDamage.DamageableType.Standard;
                    }
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
            private static bool OnDamaged_Prefix(ModuleDamage __instance, ref ManDamage.DamageInfo info)
            {
                if (__instance.GetComponent<ModuleReinforced>() != null)
                {
                    return info.Damage > 0;
                }
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
            private static void OnSpawn_Postfix(BubbleShield __instance)
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

        internal static class TankCameraPatches
        {
            internal static Type target = typeof(TankCamera);

            static FieldInfo shaker = typeof(TankCamera).GetField("m_CameraShakeTimeRemaining", BindingFlags.NonPublic | BindingFlags.Instance);
            /// <summary>
            /// GetOutOfHereCameraShakeShack
            /// </summary>=
            [HarmonyPriority(-9001)]
            private static bool SetCameraShake_Prefix(TankCamera __instance)
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
        internal static class NetPlayerPatches
        {
            internal static Type target = typeof(NetPlayer);
            private static void OnStartClient_Postfix(NetPlayer __instance)
            {
                int counter = 0;
                foreach (var item in ManModNetwork.hooks)
                {
                    if (item.Value.ClientRecieves())
                    {
                        ManNetwork.inst.SubscribeToClientMessage(__instance.netId, (TTMsgType)item.Key, new ManNetwork.MessageHandler(item.Value.OnToClientReceive_Internal));
                        DebugRandAddi.Log("Client Subscribed " + item.Value.ToString() + " to network under ID " + item.Key);
                        counter++;
                    }
                }
                DebugRandAddi.Log("Client subscribed " + counter + " hooks.");
            }
            private static void OnStartServer_Postfix(NetPlayer __instance)
            {
                if (!ManModNetwork.HostExists)
                {
                    DebugRandAddi.Log("Host started, hooked ManModNetwork update broadcasting to " + __instance.netId.ToString());
                    ManModNetwork.Host = __instance.netId;
                    ManModNetwork.HostExists = true;

                    int counter = 0;
                    foreach (var item in ManModNetwork.hooks)
                    {
                        if (item.Value.ServerRecieves()) 
                        {
                            ManNetwork.inst.SubscribeToServerMessage(__instance.netId, (TTMsgType)item.Key, new ManNetwork.MessageHandler(item.Value.OnToServerReceive_Internal));
                            DebugRandAddi.Log("Server Subscribed " + item.Value.ToString() + " to network under ID " + item.Key);
                            counter++;
                        }
                    }
                    DebugRandAddi.Log("Server subscribed " + counter + " hooks.");
                }
            }
        }

        internal static class UIDamageTypeDisplayPatches
        {
            internal static Type target = typeof(UIDamageTypeDisplay);

            static FieldInfo type = typeof(UIDamageTypeDisplay).GetField("m_DisplayType", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo name = typeof(UIDamageTypeDisplay).GetField("m_NameText", BindingFlags.NonPublic | BindingFlags.Instance); 
            static FieldInfo dispName = typeof(UIDamageTypeDisplay).GetField("m_Tooltip", BindingFlags.NonPublic | BindingFlags.Instance);

            [HarmonyPriority(-9001)]
            private static bool SetBlock_Prefix(UIDamageTypeDisplay __instance, ref BlockTypes blockType)
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
            private static bool AddRadialOpenRequest_Prefix(ManHUD __instance, ref ManHUD.HUDElementType elementType)
            {
                return elementType != UIHelpersExt.customElement;
            }
        }


        internal static class TechAudioPatches
        {
            internal static Type target = typeof(TechAudio);

            [HarmonyPriority(-9001)]
            private static bool OnModuleTickData_Prefix(ref TechAudio.AudioTickData tickData, ref FMODEvent.FMODParams additionalParam)
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
            private static void RequestReloadAllMods_Postfix(ManMods __instance)
            {
                KickStart.didQuickstart = false;
            }

            /// <summary>
            /// Insure we load SFX of corps properly!
            /// </summary>
            /// <param name="__instance"></param>
            internal static void InjectModdedCorps_Postfix(ManMods __instance)
            {
                ManMusicEnginesExt.inst.RefreshModCorpAudio();
            }
        }


    }
}
