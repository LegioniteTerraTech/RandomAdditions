using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    class PatchBatch
    {
    }

    internal static class Patches
    {
        [HarmonyPatch(typeof(Tank))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class PatchTankToHelpClocks
        {
            private static void Postfix(Tank __instance)
            {
                //Debug.Log("RandomAdditions: Patched Tank OnPool(TimeTank)");
                var ModuleAdd2 = __instance.gameObject.AddComponent<GlobalClock.TimeTank>();
                ModuleAdd2.Initiate();
            }
        }

        [HarmonyPatch(typeof(TankBlock))]
        [HarmonyPatch("PrePool")]//On Creation
        private static class PatchAllBlocksForInstaDeath
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


        [HarmonyPatch(typeof(ResourcePickup))]
        [HarmonyPatch("OnPool")]//On Creation
        private static class PatchAllChunks
        {
            private static void Postfix(ResourcePickup __instance)
            {
                //Debug.Log("RandomAdditions: Patched ResourcePickup OnPool(ModulePickupIgnore)");
                var chunk = __instance.gameObject;
                chunk.AddComponent<ItemIgnoreCollision>();
            }
        }

        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //Allow rescale of blocks on grab
        [HarmonyPatch(typeof(Visible))]
        [HarmonyPatch("SetHolder")]//On tractor grab
        private static class PatchVisibleForBlocksRescaleOnConveyor
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
                                Debug.Log("RandomAdditions: made " + __instance.name + " ignore " + firedCount + " colliders.");
                            }
                        }
                        else
                        {
                            var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
                            ModuleScale.Downscale = false;
                            ModuleScale.enabled = true;
                            //Debug.Log("RandomAdditions: Queued Rescale Up");

                            foreach (Collider collVis in __instance.gameObject.GetComponentsInChildren<Collider>())
                            {
                                //Reset them collodos
                                if (collVis.enabled)
                                {   //BUT NO TOUCH THE DISABLED ONES
                                    collVis.enabled = false;
                                    collVis.enabled = true;
                                }
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
                                __instance.gameObject.GetComponent<ItemIgnoreCollision>().UpdateCollision(true);
                            if (ModuleCheck.AllowOtherTankCollision)
                                __instance.gameObject.GetComponent<ItemIgnoreCollision>().AllowOtherTankCollisions = true;
                        }
                    }
                    else
                    {
                        __instance.gameObject.GetComponent<ItemIgnoreCollision>().UpdateCollision(false);
                    }
                }
            }
        }

        //Allow disabling of physics on mobile bases
        [HarmonyPatch(typeof(ModuleItemHolderBeam))]
        [HarmonyPatch("UpdateFloat")]//On Creation
        private static class PatchModuleItemHolderBeamForStatic
        {
            private static void Prefix(ModuleItemHolderBeam __instance, ref Visible item)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null)
                {
                    if (item.pickup.rbody != null)
                    {
                        FieldInfo lockGet = typeof(ModuleItemHolderBeam).GetField("m_HeldPhysicsItems", BindingFlags.NonPublic | BindingFlags.Instance);
                        HashSet<Visible> m_HeldPhysicsItems = (HashSet<Visible>)lockGet.GetValue(__instance);
                        if ((bool)item.pickup)
                        {
                            item.pickup.ClearRigidBody(true);
                            m_HeldPhysicsItems.Remove(item);
                        }
                        /*
                        else if ((bool)item.block)
                        {   //Unsupported
                            item.block.ClearRigidBody(true);
                            m_HeldPhysicsItems.Remove(item);
                        }
                        */
                        if (item.UsePrevHeldPos)
                        {
                            item.PrevHeldPos = WorldPosition.FromScenePosition(item.trans.position);
                        }
                        //Debug.Log("RandomAdditions: Overwrote trac beams to remain on");
                    }
                    FieldInfo scaleGet = typeof(ModuleItemHolderBeam).GetField("m_ScaleChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                    bool m_ScaleChanged = (bool)scaleGet.GetValue(__instance);

                    if (m_ScaleChanged)
                    {
                        item.trans.SetLocalScaleIfChanged(new Vector3(1f, 1f, 1f));
                        m_ScaleChanged = false; 
                        scaleGet.SetValue(__instance, m_ScaleChanged);
                    }
                    return;
                }
            }
        }

        //Allow disabling of physics on mobile bases
        [HarmonyPatch(typeof(ModuleItemHolderBeam))]
        [HarmonyPatch("OnTechAnchored")]
        private static class PatchModuleItemHolderBeamForStatic2
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

        //Override and send operations over to ModuleItemFixedHolderBeam
        [HarmonyPatch(typeof(ModuleItemHolderBeam))]
        [HarmonyPatch("SetPositionsInStack")]
        private static class PatchModuleItemHolderBeamForStatic3
        {
            private static bool Prefix(ModuleItemHolderBeam __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null)
                {
                    //__instance.
                    /*
                    FieldInfo stackGrab = typeof(ModuleItemHolderBeam).
                        GetField("m_StackData", BindingFlags.NonPublic | BindingFlags.Instance);
                    Type stackDataS = typeof(ModuleItemHolderBeam).
                        GetField("StackData", BindingFlags.NonPublic).FieldType;
                    stackDataS = stackGrab.get.GetValue(__instance);
                    for (int i = 0; i < stackData.stack.items.Count; i++)
                    {
                    }
                    */
                    if (ModuleCheck.FixateToTech)
                        return false;
                }
                return true;
            }
        }


        //Return items on crafting cancelation
        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("CancelRecipe")]
        private static class PatchModuleItemConsumerToReturn
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
        private static class PatchModuleItemConsumerResetToReturn
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
        private static class PatchModuleItemHolderStack
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
                    catch
                    {
                        otherCollider.GetComponentInParent<ModuleDeathInsurance>().TryQueueUnstoppableDeath();
                        Debug.Log("RandomAdditions: omae wa - mou shindeiru");
                    }
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

                //reportBox.Find(" Title").GetComponent<Text>().text = "<b>BUG REPORTER [MODDED!]</b>";


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
