using System;
using System.Reflection;
using System.Collections.Generic;
using Harmony;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    class PatchBatch
    {
    }

    internal class Patches
    {
        [HarmonyPatch(typeof(ModuleTechController))]
        [HarmonyPatch("ExecuteControl")]//On Control
        private class PatchControlSystem
        {
            private static bool Prefix(ModuleTechController __instance)
            {
                if (KickStart.EnableBetterAI)
                {
                    try
                    {
                        var aI = __instance.transform.root.GetComponent<Tank>().AI;
                        var tank = __instance.transform.root.GetComponent<Tank>();
                        if (!tank.PlayerFocused && aI.HasAIModules && tank.IsFriendly() && !Singleton.Manager<ManGameMode>.inst.IsCurrentModeMultiplayer())
                        {
                            //Debug.Log("RandomAdditions: (TankAIHelper) is " + tank.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>().wasEscort);
                            var tankAIHelp = tank.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
                            if (tankAIHelp.lastAIType == AITreeType.AITypes.Escort || (tankAIHelp.wasEscort && tankAIHelp.lastAIType == AITreeType.AITypes.Escort) || tankAIHelp.unanchorCountdown > 0)
                            {
                                //Debug.Log("RandomAdditions: Patched Tank ExecuteControl(TankAIHelper)");
                                tankAIHelp.BetterAI(__instance.block.tank.control);
                                return false;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("RandomAdditions: Failiure on handling AI addition!");
                        Debug.Log(e);
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]//On Creation
        private class PatchTankToHelpAIAndClocks
        {
            private static void Postfix(Tank __instance)
            {
                //Debug.Log("RandomAdditions: Patched Tank OnPool(TankAIHelper & TimeTank)");
                var ModuleAdd2 = __instance.gameObject.AddComponent<GlobalClock.TimeTank>();
                ModuleAdd2.Initiate();
                var ModuleAdd = __instance.gameObject.AddComponent<AI.AIEnhancedCore.TankAIHelper>();
                ModuleAdd.Subscribe(__instance);
                __instance.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>();
            }
        }

        /*
        [HarmonyPatch(typeof(TankBeam))]
        [HarmonyPatch("Update")]//Give the AI some untangle help
        private class PatchTankBeamToHelpAI
        {
            private static void Postfix(TankBeam __instance)
            {
                //Debug.Log("RandomAdditions: Patched TankBeam Update(TankAIHelper)");
                var ModuleCheck = __instance.gameObject.GetComponent<AIEnhancedCore.TankAIHelper>();
                if (ModuleCheck != null)
                {
                }
            }
        }
        */

        [HarmonyPatch(typeof(TankBlock))]
        [HarmonyPatch("PrePool")]//On Creation
        private class PatchAllBlocksForInstaDeath
        {
            private static void Postfix(TankBlock __instance)
            {
                //Debug.Log("RandomAdditions: Patched TankBlock OnPool(ModuleDeathInsurance & TankBlockScaler)");
                var block = __instance.gameObject;
                block.AddComponent<ModuleDeathInsurance>();
                if (!(bool)block.GetComponent<TankBlockScaler>())
                {   //This allows for an override to be concocted if the block maker wants to specify a custom size
                    var setComp = block.AddComponent<TankBlockScaler>();
                    var bound = __instance.BlockCellBounds.extents;
                    setComp.AimedDownscale = Mathf.Min(Mathf.Max(0.001f, 1 / Mathf.Max(Mathf.Max(bound.x, bound.y), bound.z)) / 2, 0.5f);
                }
                var run = block.GetComponent<TankBlockScaler>();
                run.OnPool();
                run.enabled = false;
            }
        }

        [HarmonyPatch(typeof(ModuleAIBot))]
        [HarmonyPatch("OnPool")]//On Creation
        private class ImproveAI
        {
            private static void Postfix(ModuleAIBot __instance)
            {
                var valid = __instance.GetComponent<AI.AIEnhancedCore.ModuleAIExtension>();
                if (valid)
                {
                    valid.OnPool();
                }
                else
                {
                    var ModuleAdd = __instance.gameObject.AddComponent<AI.AIEnhancedCore.ModuleAIExtension>();
                    ModuleAdd.OnPool();
                    // Now retrofit AIs
                    try
                    {
                        var name = __instance.gameObject.name;
                        if (name == "GSO_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                        }
                        if (name == "GSO_AIAnchor_121")
                        {
                            ModuleAdd.Aegis = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "GC_AI_Module_Guard_222")
                        {
                            ModuleAdd.Prospector = true;
                            ModuleAdd.MeleePreferred = true;
                        }
                        else if (name == "VEN_AI_Module_Guard_111")
                        {
                            ModuleAdd.Aviator = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.CirclePreferred = true;
                            ModuleAdd.MaxCombatRange = 300;
                        }
                        else if (name == "HE_AI_Module_Guard_112")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.MaxCombatRange = 200;
                        }
                        else if (name == "HE_AI_Turret_111")
                        {
                            ModuleAdd.Assault = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.MaxCombatRange = 150;
                        }
                        else if (name == "BF_AI_Module_Guard_212")
                        {
                            ModuleAdd.Astrotech = true;
                            ModuleAdd.Prospector = true;//Temp until main intended function arrives
                            ModuleAdd.AdvAvoidence = true;
                            ModuleAdd.MaxCombatRange = 175;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.Log("RandomAdditions: CRASH ON HANDLING EXISTING AIS");
                        Debug.Log(e);
                    }
                }
            }
        }


        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnSpawn")]//On World Spawn
        private class PatchResourcesToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("RandomAdditions: Added resource to list (OnSpawn)");
                if (!AI.AIEnhancedCore.Minables.Contains(__instance.transform))
                    AI.AIEnhancedCore.Minables.Add(__instance.transform);
                else
                    Debug.Log("RandomAdditions: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Regrow")]//On World Spawn
        private class PatchResourceRegrowToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("RandomAdditions: Added resource to list (OnSpawn)");
                if (!AI.AIEnhancedCore.Minables.Contains(__instance.transform))
                    AI.AIEnhancedCore.Minables.Add(__instance.transform);
                //else
                //    Debug.Log("RandomAdditions: RESOURCE WAS ALREADY ADDED! (OnSpawn)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("Die")]//On resource destruction
        private class PatchResourceDeathToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("RandomAdditions: Removed resource from list (Die)");
                if (AI.AIEnhancedCore.Minables.Contains(__instance.transform))
                {
                    AI.AIEnhancedCore.Minables.Remove(__instance.transform);
                }
                else
                    Debug.Log("RandomAdditions: RESOURCE WAS ALREADY REMOVED! (Die)");
            }
        }

        [HarmonyPatch(typeof(ResourceDispenser))]
        [HarmonyPatch("OnRecycle")]//On World Destruction
        private class PatchResourceRecycleToHelpAI
        {
            private static void Prefix(ResourceDispenser __instance)
            {
                //Debug.Log("RandomAdditions: Removed resource from list (OnRecycle)");
                if (AI.AIEnhancedCore.Minables.Contains(__instance.transform))
                {
                    AI.AIEnhancedCore.Minables.Remove(__instance.transform);
                }
                //else
                //    Debug.Log("RandomAdditions: RESOURCE WAS ALREADY REMOVED! (OnRecycle)");

            }
        }

        [HarmonyPatch(typeof(ModuleItemPickup))]
        [HarmonyPatch("OnPool")]//On Creation
        private class MarkReceiver
        {
            private static void Postfix(ModuleItemPickup __instance)
            {
                var valid = __instance.GetComponent<ModuleItemHolder>();
                if (valid)
                {
                    if (valid.IsFlag(ModuleItemHolder.Flags.Receiver))
                    {
                        var ModuleAdd = __instance.gameObject.AddComponent<ModuleHarvestReciever>();
                        ModuleAdd.OnPool();
                    }
                }
            }
        }

        /*
        [HarmonyPatch(typeof(TechAI))]
        [HarmonyPatch("SetCurrentTree")]//On Creation
        private class DetectAIChangePatch
        {
            private static void Prefix(TechAI __instance, ref AITreeType aiTreeType)
            {
                if (aiTreeType != null)
                {
                    FieldInfo currentTreeActual = typeof(TechAI).GetField("m_CurrentAITreeType", BindingFlags.NonPublic | BindingFlags.Instance);
                    if ((AITreeType)currentTreeActual.GetValue(__instance) != aiTreeType)
                    {
                        //
                    }
                }
            }
        }
        */

        [HarmonyPatch(typeof(RadialMenu))]
        [HarmonyPatch("InitMenu")]//On Creation
        private class DetectAIRadialAction
        {
            private static void Prefix(RadialMenu __instance)
            {
                GUIAIManager.GetTank();
            }
        }


        [HarmonyPatch(typeof(UIRadialMenuOption))]//UIRadialMenuOptionWithWarning
        [HarmonyPatch("IsInside")]//On AI option
        private class DetectAIRadialMenuAction
        {
            private static void Prefix(UIRadialMenuOption __instance)
            {
                if (__instance.gameObject.name == "Bottom_Left_Button")
                {
                    if (__instance.gameObject.GetComponent<UIRadialMenuOptionWithWarning>())
                        GUIAIManager.LaunchSubMenuClickable();
                }
            }
        }

        [HarmonyPatch(typeof(TankControl))]
        [HarmonyPatch("CopySchemesFrom")]//On Split
        private class SetMTAIAuto
        {
            private static void Prefix(TankControl __instance, ref TankControl other)
            {
                if (__instance.Tech.blockman.IterateBlockComponents<ModuleWheels>().Count() > 0 || __instance.Tech.blockman.IterateBlockComponents<ModuleHover>().Count() > 0)
                    __instance.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>().DediAI = AI.AIEnhancedCore.DediAIType.Escort;
                else
                {
                    if (__instance.Tech.blockman.IterateBlockComponents<ModuleWeapon>().Count() > 0)
                        __instance.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>().DediAI = AI.AIEnhancedCore.DediAIType.MTTurret;
                    else
                        __instance.gameObject.GetComponent<AI.AIEnhancedCore.TankAIHelper>().DediAI = AI.AIEnhancedCore.DediAIType.MTSlave;
                }
            }
        }
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //Allow rescale of blocks on grab
        [HarmonyPatch(typeof(Visible))]
        [HarmonyPatch("SetHolder")]//On tractor grab
        private class PatchVisibleForBlocksRescaleOnConveyor
        {
            private static void Prefix(Visible __instance, ref ModuleItemHolder.Stack stack)
            {
                if ((bool)__instance.block)
                {
                    if (!__instance.block.IsAttached)
                    {
                        //Debug.Log("RandomAdditions: Overwrote visible to handle resources");
                        if (stack != null)
                        {
                            var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
                            ModuleScale.Downscale = true;
                            ModuleScale.enabled = true;
                            //Debug.Log("RandomAdditions: Queued Rescale Down");
                        }
                        else
                        {
                            var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
                            ModuleScale.Downscale = false;
                            ModuleScale.enabled = true;
                            //Debug.Log("RandomAdditions: Queued Rescale Up");
                        }
                    }
                }
            }
        }

        //Allow disabling of physics on mobile bases
        [HarmonyPatch(typeof(ModuleItemHolderBeam))]
        [HarmonyPatch("UpdateFloat")]//On Creation
        private class PatchModuleItemHolderBeamForStatic
        {
            private static void Prefix(ModuleItemHolderBeam __instance, ref Visible item)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null && item.pickup.rbody != null)
                {
                    FieldInfo lockGet = typeof(ModuleItemHolderBeam).GetField("m_HeldPhysicsItems", BindingFlags.NonPublic | BindingFlags.Instance);
                    HashSet <Visible> m_HeldPhysicsItems = (HashSet<Visible>)lockGet.GetValue(__instance);

                    if ((bool)item.pickup)
                    {
                        item.pickup.ClearRigidBody(immediate: true);
                    }
                    else if ((bool)item.block)
                    {
                        item.block.ClearRigidBody(immediate: true);
                    }
                    m_HeldPhysicsItems.Remove(item);
                    if (item.UsePrevHeldPos)
                    {
                        item.PrevHeldPos = WorldPosition.FromScenePosition(item.trans.position);
                    }
                    //Debug.Log("RandomAdditions: Overwrote trac beams to remain on");
                }
            }
        }

        //Allow disabling of physics on mobile bases
        [HarmonyPatch(typeof(ModuleItemHolderBeam))]
        [HarmonyPatch("OnTechAnchored")]//On Creation
        private class PatchModuleItemHolderBeamForStatic2
        {
            private static bool Prefix(ModuleItemHolderBeam __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null)
                {
                    //Debug.Log("RandomAdditions: Overwrote trac beams to remain on");
                    return false;
                }
                return true;
            }
        }

        //Return items on crafting cancelation
        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("CancelRecipe")]//On Creation
        private class PatchModuleItemConsumerToReturn
        {
            private static void Prefix(ModuleItemConsume __instance)
            {
                FieldInfo recipeGetS = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                ModuleItemConsume.Progress fetchedRecipeS = (ModuleItemConsume.Progress)recipeGetS.GetValue(__instance);
                RecipeTable.Recipe fetchedRecipe = fetchedRecipeS.currentRecipe;
                FieldInfo ejectGet = typeof(ModuleItemConsume).GetField("m_KickLocator", BindingFlags.NonPublic | BindingFlags.Instance);
                Transform ejectorTransform = (Transform)ejectGet.GetValue(__instance);

                try
                {
                    for (int C = 0; C < fetchedRecipe.m_InputItems.Length; C++)
                    {
                        ItemTypeInfo compare = fetchedRecipe.m_InputItems[C].m_Item;
                        int totRequest = fetchedRecipe.m_InputItems[C].m_Quantity;
                        List<ItemTypeInfo> notAvailR = __instance.InputsRemaining.FindAll(delegate (ItemTypeInfo cand) { return cand == compare; });
                        int notAvail = notAvailR.Count;

                        //Debug.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                        int ToReturn = totRequest - notAvail;
                        //Debug.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
                        for (int E = 0; E < ToReturn; E++)
                        {
                            var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(compare, ejectorTransform.position, Quaternion.identity);
                            itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                        }
                    }
                }
                catch
                {
                    Debug.Log("RandomAdditions: CONSUME - Nothing more to eject.");
                }
            }
        }

        //Return items on fabgrab
        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("ResetState")]//On Creation
        private class PatchModuleItemConsumerResetToReturn
        {
            private static void Prefix(ModuleItemConsume __instance)
            {
                FieldInfo recipeGetS = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                ModuleItemConsume.Progress fetchedRecipeS = (ModuleItemConsume.Progress)recipeGetS.GetValue(__instance);
                RecipeTable.Recipe fetchedRecipe = fetchedRecipeS.currentRecipe;
                FieldInfo ejectGet = typeof(ModuleItemConsume).GetField("m_KickLocator", BindingFlags.NonPublic | BindingFlags.Instance);
                Transform ejectorTransform = (Transform)ejectGet.GetValue(__instance);

                try
                {
                    for (int C = 0; C < fetchedRecipe.m_InputItems.Length; C++)
                    {
                        ItemTypeInfo compare = fetchedRecipe.m_InputItems[C].m_Item;
                        int totRequest = fetchedRecipe.m_InputItems[C].m_Quantity;
                        List<ItemTypeInfo> notAvailR = __instance.InputsRemaining.FindAll(delegate (ItemTypeInfo cand) { return cand == compare; });
                        int notAvail = notAvailR.Count;

                        //Debug.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                        int ToReturn = totRequest - notAvail;
                        //Debug.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
                        for (int E = 0; E < ToReturn; E++)
                        {
                            var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(compare, ejectorTransform.position, Quaternion.identity);
                            itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                        }
                    }
                }
                catch
                {
                    //Debug.Log("RandomAdditions: CONSUME INPUTS - Nothing more to eject.");
                }
                try
                {
                    int fireTimesOut = fetchedRecipeS.outputQueue.Count;
                    for (int C = 0; C < fireTimesOut; C++)
                    {
                        ItemTypeInfo toEject = fetchedRecipeS.outputQueue.Pop();
                        var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(toEject, ejectorTransform.position, Quaternion.identity);
                        itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                    }
                }
                catch
                {
                    //Debug.Log("RandomAdditions: CONSUME OUTPUTS - Nothing more to eject.");
                }

            }
        }

        //Handle internal silo stacks and input fake values
        [HarmonyPatch(typeof(ModuleItemHolder.Stack))]
        [HarmonyPatch("OfferAllItemsToCollector")]//On Creation
        private class PatchModuleItemHolderStack
        {
            private static void Prefix(ModuleItemHolder.Stack __instance, ref ItemSearchCollector collector)
            {
                var ModuleCheck = __instance.myHolder.gameObject.GetComponent<ModuleItemSilo>();
                if (ModuleCheck != null)
                {
                    if (!ModuleCheck.WasSearched)
                    {
                        int count = ModuleCheck.SavedCount;
                        if (count > 0)
                        {
                            if (__instance.items[0].m_ItemType.ObjectType == ObjectTypes.Chunk)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    collector.OfferAnonItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)__instance.items[0].pickup.ChunkType));
                                    //Debug.Log("RandomAdditions: Added " + __instance.items[0].pickup.ChunkType.ToString() + " to available recipie items");
                                }
                                //Debug.Log("RandomAdditions: Searched silo (Chunks)");
                            }
                            else
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    try
                                    {
                                        collector.OfferAnonItem(new ItemTypeInfo(ObjectTypes.Block, (int)__instance.items[0].block.BlockType));
                                    }
                                    catch { }// Chances are we can't get modded blocks with this
                                }
                                //Debug.Log("RandomAdditions: Searched silo (Blocks)");
                            }
                            ModuleCheck.WasSearched = true;
                        }
                    }
                }
            }
        }


        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------

        //Make sure that WeightedProjectile is checked for and add changes
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("PrePool")]//On Creation
        private class PatchProjectilePre
        {
            private static void Postfix(Projectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched Projectile OnPool(LanceProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<LanceProjectile>();
                if (ModuleCheck != null)
                {
                    FieldInfo collodo = typeof(Projectile).GetField("m_Collider", BindingFlags.NonPublic | BindingFlags.Instance);
                    Collider fetchedCollider = (Collider)collodo.GetValue(__instance);
                    fetchedCollider.isTrigger = true;// Make it not collide
                    ModuleCheck.project = __instance;
                    Debug.Log("RandomAdditions: Overwrote Collision");
                }
            }
        }

        //Make sure that WeightedProjectile is checked for and add changes
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("OnPool")]//On Creation
        private class PatchProjectile
        {
            private static void Postfix(Projectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched Projectile OnPool(WeightedProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<WeightedProjectile>();
                if (ModuleCheck != null)
                {
                    ModuleCheck.SetProjectileMass();
                    Debug.Log("RandomAdditions: Overwrote Mass");
                }
            }
        }

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("HandleCollision")]//On direct hit
        private class PatchProjectileCollision
        {
            private static void Prefix(Projectile __instance, ref Collider otherCollider)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //Debug.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile & OHKOProjectile)");
                var ModuleCheckS = __instance.gameObject.GetComponent<KeepSeekingProjectile>();
                if (ModuleCheckS != null)
                {
                    var validation = __instance.gameObject.GetComponent<SeekingProjectile>();
                    if (validation)
                    {
                        ModuleCheckS.wasThisSeeking = validation.enabled; //Keep going!
                    }
                }
                var ModuleCheck = __instance.gameObject.GetComponent<OHKOProjectile>();
                if (ModuleCheck != null)
                {
                    var validation = otherCollider.GetComponentInParent<ModuleDeathInsurance>();
                    if (!validation)
                    {
                        //Debug.Log("RandomAdditions: did not hit possible block");
                        return;
                    }

                    if (__instance.Shooter.IsFriendly(otherCollider.GetComponent<TankBlock>().tank.Team) || otherCollider.GetComponent<TankBlock>().tank.IsNeutral())
                        return;// Stop friendly-fire

                    //Debug.Log("RandomAdditions: queued block death");
                    try
                    {
                        otherCollider.GetComponent<ModuleDeathInsurance>().TryQueueUnstoppableDeath();
                        Debug.Log("RandomAdditions: omae wa - mou shindeiru");
                        return;
                    }
                    catch { }
                    otherCollider.GetComponentInParent<ModuleDeathInsurance>().TryQueueUnstoppableDeath();
                    Debug.Log("RandomAdditions: omae wa - mou shindeiru");
                    //Singleton.Manager<ManDamage>.inst.DealDamage(damageable, m_Damage, m_DamageType, m_Weapon, Shooter, hitPoint, __instance.rbody.velocity);
                    //__instance.Recycle(worldPosStays: false);
                }
                else
                {
                    //Debug.Log("RandomAdditions: let block live");
                }
            }
        }

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("HandleCollision")]//On direct hit
        private class PatchProjectileCollisionForOverride
        {
            private static void Postfix(Projectile __instance, ref Collider otherCollider)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //Debug.Log("RandomAdditions: Patched Projectile HandleCollision(KeepSeekingProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<KeepSeekingProjectile>();
                if (ModuleCheck != null)
                {
                    var validation = __instance.gameObject.GetComponent<SeekingProjectile>();
                    if (validation)
                    {
                        validation.enabled = ModuleCheck.wasThisSeeking; //Keep going!
                    }
                    else
                    {
                        Debug.Log("RandomAdditions: Projectile " + __instance.name + " Does not have a SeekingProjectile to go with KeepSeekingProjectile!");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Fire")]//On Fire
        private class PatchProjectileFire
        {
            private static void Postfix(Projectile __instance, ref Tank shooter)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //Debug.Log("RandomAdditions: Patched Projectile Fire(WeightedProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<WeightedProjectile>();
                if (ModuleCheck != null)
                {
                    if (shooter != null && ModuleCheck.CustomGravity)
                    {
                        Vector3 final = ((__instance.rbody.velocity - shooter.rbody.velocity) * ModuleCheck.GravityAndSpeedScale) + shooter.rbody.velocity;
                        Debug.Log("RandomAdditions: Scaled WeightedProjectile Speed from " + __instance.rbody.velocity + " to " + final);
                        __instance.rbody.velocity = final;
                    }
                }
                var ModuleCheck2 = __instance.gameObject.GetComponent<TorpedoProjectile>();
                if (ModuleCheck2 != null)
                {
                    ModuleCheck2.OnFire();
                }
            }
        }

        //Add torpedo functionality
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("DeactivateBoosters")]
        private class PatchMissileProjectileEnd
        {
            private static void Postfix(Projectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched MissileProjectile DeactivateBoosters(TorpedoProjectile)");
                if (KickStart.isWaterModPresent)
                {
                    var ModuleCheck = __instance.gameObject.GetComponent<TorpedoProjectile>();
                    if (ModuleCheck != null)
                    {
                        ModuleCheck.KillSubmergedThrust();
                    }
                }
            }
        }

        // Allow blocks to have one special resistance
        [HarmonyPatch(typeof(Damageable))]
        [HarmonyPatch("Damage")]//On damage handling
        private class PatchDamageable
        {
            private static void Prefix(Damageable __instance, ref ManDamage.DamageInfo info)
            {
                //Debug.Log("RandomAdditions: Patched Damageable Damage(ModuleReinforced)");
                var modifPresent = __instance.gameObject.GetComponent<ModuleReinforced>();
                if (modifPresent != null)
                {
                    if (modifPresent.ModifyAoEDamage && info.Source.GetComponent<Explosion>().IsNotNull())
                    {
                        info.ApplyDamageMultiplier(modifPresent.AoEMultiplier);
                    }
                    if (modifPresent.UseMultipliers)
                    {
                        var multi = __instance.gameObject.GetComponent<ModuleReinforced>();
                        switch (info.DamageType)
                        {
                            case ManDamage.DamageType.Standard:
                                info.ApplyDamageMultiplier(multi.Standard);
                                return;
                            case ManDamage.DamageType.Bullet:
                                info.ApplyDamageMultiplier(multi.Bullet);
                                return;
                            case ManDamage.DamageType.Energy:
                                info.ApplyDamageMultiplier(multi.Energy);
                                return;
                            case ManDamage.DamageType.Explosive:
                                info.ApplyDamageMultiplier(multi.Explosive);
                                return;
                            case ManDamage.DamageType.Impact:
                                info.ApplyDamageMultiplier(multi.Impact);
                                return;
                            case ManDamage.DamageType.Cutting:
                                info.ApplyDamageMultiplier(multi.Cutting);
                                return;
                            case ManDamage.DamageType.Fire:
                                info.ApplyDamageMultiplier(multi.Fire);
                                return;
                            case ManDamage.DamageType.Plasma:
                                info.ApplyDamageMultiplier(multi.Plasma);
                                return;
                        }
                        Debug.Log("RandomAdditions: !! NEW DAMAGE TYPE DETECTED !!   ALERT CODER!!!");
                        Debug.Log("RandomAdditions: Type " + info.DamageType.ToString());
                        Debug.Log("RandomAdditions: Update ModuleReinforced and also Patch!");
                        //CRASH THE GAME!
                        LogHandler.ForceCrashReporterCustom("!!NEW DAMAGE TYPE DETECTED!!   ALERT CODER!!!\nType " + info.DamageType.ToString() + "\nUpdate ModuleReinforced and also PatchBatch!");
                    }




                    /*
                    if (info.DamageType == modifPresent.TypeToResist)
                    {
                        if (modifPresent.IsResistedProof)
                        {
                            //Debug.Log("RandomAdditions: Damage denied");
                            info.ApplyDamageMultiplier(0f);//take zero-zilch damage from the type to resist
                        }
                        else
                        {
                            //Debug.Log("RandomAdditions: Damage weakened");
                            info.ApplyDamageMultiplier(modifPresent.ResistMultiplier);
                        }
                    }
                    else if (modifPresent.DoDamagablePenalty)
                    {
                        info.ApplyDamageMultiplier(Mathf.Max(modifPresent.PenaltyMultiplier, 1));
                    }
                    */
                }
            }
        }

        [HarmonyPatch(typeof(Damageable))]
        [HarmonyPatch("OnPool")]//On damage handling
        private class PatchDamageableChange
        {
            private static void Prefix(Damageable __instance)
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


        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------

        [HarmonyPatch(typeof(UIScreenBugReport))]
        [HarmonyPatch("Show")]//On error screen text field
        private class DisableCrashTextMenu
        {
            private static void Postfix(UIScreenBugReport __instance)
            {
                //Custom error menu
                Debug.Log("RandomAdditions: DISABLED");
                return; //end it before it can display the text field
            }
        }

        [HarmonyPatch(typeof(UIScreenBugReport))]
        [HarmonyPatch("Set")]//On error screen
        private class OverhaulCrashPatch
        {
            private static void Postfix(UIScreenBugReport __instance)
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

                UIObj.transform.Find("ReportLayout").Find("Button Forward").Find("Text").GetComponent<Text>().text = "(CORRUPTION WARNING) CONTINUE ANYWAYS";
                //Debug.Log("RandomAdditions: Cleaned Bug Reporter UI");

                //Setup the UI
                string outputLogLocation = "(Error on OS fetch request)";
                switch (SystemInfo.operatingSystemFamily)
                {
                    case OperatingSystemFamily.Windows:
                        outputLogLocation = "%LOCALAPPDATA%\\Payload\\TerraTech\\output_log.txt";
                        break;
                    case OperatingSystemFamily.MacOSX:
                        outputLogLocation = "~/Library/Logs/TerraTech/output_log.txt";
                        break;
                    case OperatingSystemFamily.Linux:
                        outputLogLocation = "Uhh,  please notify on the discord";
                        break;
                    case OperatingSystemFamily.Other:
                        outputLogLocation = "Uhh,  please notify on the discord";
                        break;
                }

                try
                {   //<color=#f23d3dff></color> - tried that but it's too hard to read
                    string latestError = KickStart.logMan.GetComponent<LogHandler>().GetErrors();
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b>  \nTake note of all your unofficial mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial. \n\nThe log file is at: " + outputLogLocation;

                    var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Explanation"), UIObj.transform, false);
                    Vector3 offset = errorList.localPosition;
                    offset.y = offset.y - 340;
                    errorList.localPosition = offset;
                    var errorText = errorList.gameObject.GetComponent<Text>();
                    //errorText.alignByGeometry = true;
                    //errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.fontSize = 16;
                    errorText.text = "<b>Error:</b> " + latestError;
                    errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
                    errorText.verticalOverflow = VerticalWrapMode.Overflow;
                    //bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<color=#f23d3dff>Please DON'T press</color> <b>SEND</b>!  <color=#f23d3dff>This is a MODDED GAME AND THAT DOESN'T WORK!</color>  \nYou can skip this screen with the button at the upper-left corner and continue in your world, but do remember you are putting your save at risk! \n\nError: ";
                }
                catch
                {
                    Debug.Log("RandomAdditions: FAILIURE ON FETCHING LOG!");
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n\n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b> \nTake note of all your unofficial mods and send the attached Bug Report below in the Official TerraTech Discord, in #modding-unofficial. \n\nThe log file is at: " + outputLogLocation;
                    var errorList = UnityEngine.Object.Instantiate(reportBox.Find("Explanation"), UIObj.transform, false);
                    Vector3 offset = errorList.localPosition;
                    offset.y = offset.y - 340;
                    errorList.localPosition = offset;
                    var errorText = errorList.gameObject.GetComponent<Text>();
                    //errorText.alignByGeometry = true;
                    //errorText.alignment = TextAnchor.UpperLeft; // causes it to go far out of the box
                    errorText.fontSize = 50;
                    errorText.text = "<b>COULD NOT FETCH ERROR!!!</b>";
                }
            }
        }
    }
}
