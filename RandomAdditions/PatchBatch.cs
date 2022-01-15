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
        // Major Patches
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
        private static class PatchAllBlocksForOHKOProjectile
        {
            private static void Postfix(TankBlock __instance)
            {
                //Debug.Log("RandomAdditions: Patched TankBlock OnPool(ModuleDeathInsurance & TankBlockScaler)");
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
        // Custom Block Modules
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
                            if (KickStart.AutoScaleBlocksInSCU || !stack.myHolder.gameObject.GetComponent<ModuleHeart>())
                            {
                                var ModuleScale = __instance.gameObject.GetComponent<TankBlockScaler>();
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
            private static bool Prefix(ModuleItemHolderBeam __instance, ref Visible item)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null)
                {
                    if (item.rbody != null)
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
                    return false;
                }
                else
                {
                    try
                    {
                        if (!__instance.block.tank.IsAnchored && item.rbody == null)
                        {
                            item.pickup.InitRigidbody();
                        }
                    }
                    catch { }
                }
                return true;
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

        //Let BurnerJet do it's job accurately
        [HarmonyPatch(typeof(BoosterJet))]
        [HarmonyPatch("FixedUpdate")]
        private static class RunModuleBoosterBurner
        {
            private static void Prefix(BoosterJet __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<BurnerJet>();
                if (ModuleCheck != null)
                {
                    if (!ModuleCheck.isSetup)
                        ModuleCheck.Initiate(__instance);
                    ModuleCheck.Run(__instance.IsFiring);
                }
            }
        }

        // Disable firing when intercepting
        [HarmonyPatch(typeof(ModuleWeapon))]
        [HarmonyPatch("Process")]
        private static class ProcessOverride
        {
            private static bool Prefix(ModuleWeapon __instance, ref int __result)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModulePointDefense>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.UsingWeapon)
                    {
                        __result = 0;
                        return false;
                    }
                }
                return true;
            }
        }

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

        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // Try make crafting a bit more bearable

        //Return items on crafting cancelation
        [HarmonyPatch(typeof(ModuleItemConsume))]
        [HarmonyPatch("CancelRecipe")]
        private static class PatchModuleItemConsumerToReturnInputs
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
                    if (ejectorTransform != null)
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
                    else
                    {   // Else it has the resources coming out of an Output
                        int fireTimesOut = fetchedRecipeS.outputQueue.Count;
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
                                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(compare, __instance.transform.position, Quaternion.identity);
                                itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                            }   // we just assume that the output is upright
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
        private static class PatchModuleItemConsumerToReturnOnReset
        {
            private static void Prefix(ModuleItemConsume __instance)
            {
                FieldInfo recipeGetS = typeof(ModuleItemConsume).GetField("m_ConsumeProgress", BindingFlags.NonPublic | BindingFlags.Instance);
                ModuleItemConsume.Progress fetchedRecipeS = (ModuleItemConsume.Progress)recipeGetS.GetValue(__instance);
                RecipeTable.Recipe fetchedRecipe = fetchedRecipeS.currentRecipe;
                FieldInfo ejectGet = typeof(ModuleItemConsume).GetField("m_KickLocator", BindingFlags.NonPublic | BindingFlags.Instance);
                Transform ejectorTransform = (Transform)ejectGet.GetValue(__instance);
                FieldInfo isFinished = typeof(ModuleItemConsume).GetField("OperatingBeatsLeft", BindingFlags.NonPublic | BindingFlags.Instance);
                

                try
                {
                    if (ejectorTransform != null)
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
                                if (compare.ObjectType == ObjectTypes.Block)
                                {
                                    var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnBlock((BlockTypes)compare.ItemType, ejectorTransform.position, Quaternion.identity);
                                    itemSpawn.InitNew();
                                    itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                                }
                                else
                                {
                                    var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(compare, ejectorTransform.position, Quaternion.identity);
                                    itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                                }
                            }
                        }
                    }
                    else
                    {   // Else it has the resources coming out of an Output
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
                                if (compare.ObjectType == ObjectTypes.Block)
                                {
                                    var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnBlock((BlockTypes)compare.ItemType, __instance.transform.position, Quaternion.identity);
                                    itemSpawn.InitNew();
                                    itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                                }
                                else
                                {
                                    var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(compare, __instance.transform.position, Quaternion.identity);
                                    itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                                }
                            }
                        }
                    }
                }
                catch
                {
                    //Debug.Log("RandomAdditions: CONSUME INPUTS - Nothing more to eject.");
                }
                /* // Output emergency throwout -  doesn't work as the devs mess something up down the line
                 * //   And null something that they can still pull somehow on their end
                try
                {   // Output emergency throwout
                    //if ((int)isFinished.GetValue(__instance) == 1)
                    //{
                    //    Debug.Log("RandomAdditions: CONSUME (Output throwout) - Queued");
                    //}
                    Debug.Log("RandomAdditions: CONSUME (Output throwout) - Queued");
                    if (ejectorTransform != null)
                    {
                        Debug.Log("RandomAdditions: CONSUME (Output throwout) - what");
                        Stack<ItemTypeInfo> reMatch = new Stack<ItemTypeInfo>();

                        for (int Queue = fetchedRecipe.m_OutputItems.Length - 1; Queue >= 0; Queue--)
                        {   // shove to the queue to shove out all at once
                            RecipeTable.Recipe.ItemSpec itemSpec = fetchedRecipe.m_OutputItems[Queue];
                            for (int N = 0; N < itemSpec.m_Quantity; N++)
                            {
                                reMatch.Push(itemSpec.m_Item);
                            }
                        }
                        int fireTimesOut = reMatch.Count;
                        Debug.Log("RandomAdditions: CONSUME (Output throwout) - " + fireTimesOut + " items in reserve");
                        for (int C = 0; C < fireTimesOut; C++)
                        {
                            ItemTypeInfo toEject = reMatch.Pop();
                            if (toEject.ObjectType == ObjectTypes.Block)
                            {
                                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnBlock((BlockTypes)toEject.ItemType, ejectorTransform.position, Quaternion.identity);
                                itemSpawn.InitNew();
                                itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                            }
                            else
                            {
                                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(toEject, ejectorTransform.position, Quaternion.identity);
                                itemSpawn.rbody.AddRandomVelocity(ejectorTransform.forward * 12, Vector3.one * 5, 30);
                            }
                        }
                    }
                    else
                    {   // Else it has the resources coming out of an Output
                        Debug.Log("RandomAdditions: CONSUME (Output throwout) - 0");
                        Stack<ItemTypeInfo> reMatch = new Stack<ItemTypeInfo>();
                        Debug.Log("RandomAdditions: CONSUME (Output throwout) - 0.5");

                        Debug.Log("RandomAdditions: CONSUME (Output throwout) - 1");
                        int fireTimesOut;
                        try
                        {
                            int queuepre = fetchedRecipe.m_OutputItems.Length;
                            Debug.Log("RandomAdditions: CONSUME (Output throwout) - count " + queuepre);
                            for (int Queue = queuepre - 1; Queue >= 0; Queue--)
                            {   // shove to the queue to shove out all at once

                                _ = reMatch.Count;
                                Debug.Log("RandomAdditions: CONSUME (Output throwout) - 2");
                                RecipeTable.Recipe.ItemSpec itemSpec = fetchedRecipe.m_OutputItems[Queue];
                                for (int N = 0; N < itemSpec.m_Quantity; N++)
                                {
                                    Debug.Log("RandomAdditions: CONSUME (Output throwout) - 3");
                                    reMatch.Push(itemSpec.m_Item);
                                }
                            }
                            fireTimesOut = reMatch.Count;
                        }
                        catch 
                        {
                            fireTimesOut = fetchedRecipeS.outputQueue.Count;
                        }
                        Debug.Log("RandomAdditions: CONSUME (Output throwout - Fallback) - " + fireTimesOut + " items in reserve");
                        for (int C = 0; C < fireTimesOut; C++)
                        {
                            ItemTypeInfo toEject = reMatch.Pop();
                            if (toEject.ObjectType == ObjectTypes.Block)
                            {
                                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnBlock((BlockTypes)toEject.ItemType, __instance.transform.position, Quaternion.identity);
                                itemSpawn.InitNew();
                                itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                            }
                            else
                            {
                                var itemSpawn = Singleton.Manager<ManSpawn>.inst.SpawnItem(toEject, __instance.transform.position, Quaternion.identity);
                                itemSpawn.rbody.AddRandomVelocity(Vector3.up * 12, Vector3.one * 5, 30);
                            }
                        }
                    }
                }
                catch (Exception e)
                {

                    Debug.Log("RandomAdditions: CONSUME OUTPUTS - " + e);
                    //Debug.Log("RandomAdditions: CONSUME OUTPUTS - Nothing more to eject.");
                }
                */
            }
        }

        //Handle internal silo stacks and input fake values
        [HarmonyPatch(typeof(ModuleItemHolder.Stack))]
        [HarmonyPatch("OfferAllItemsToCollector")]//On Creation
        private static class PatchModuleItemHolderStackToSeeInternalSiloContents
        {
            private static void Postfix(ModuleItemHolder.Stack __instance, ref ItemSearchCollector collector)
            {   
                var ModuleCheck = __instance.myHolder.gameObject.GetComponent<ModuleItemSilo>();
                if (ModuleCheck != null)
                {
                    if (!ModuleCheck.WasSearched)
                    {
                        int count = ModuleCheck.SavedCount;
                        if (count > 0)
                        {
                            if (!ModuleCheck.StoresBlocksInsteadOfChunks)
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    collector.OfferAnonItem(new ItemTypeInfo(ObjectTypes.Chunk, (int)ModuleCheck.GetChunkType));
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
                                        collector.OfferAnonItem(new ItemTypeInfo(ObjectTypes.Block, (int)ModuleCheck.GetBlockType));
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

        [HarmonyPatch(typeof(ManPop))]
        [HarmonyPatch("OnSpawned")]//On enemy base bomb landing
        private static class PerformBlocksSwapOperation
        {
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

            private static void Postfix(ref TrackedVisible tv)
            {   // Change the parts on the Tech with blocks with ModuleReplace attached
                if (!TankExists(tv))
                    return;
                Tank tank = tv.visible.tank;
                ReplaceManager.TryReplaceBlocks(tank);
            }
        }


        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // Custom Projectiles

        //Make sure that WeightedProjectile is checked for and add changes
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("PrePool")]//On Creation
        private class PatchProjectilePre
        {
            static FieldInfo collodo = typeof(Projectile).GetField("m_Collider", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Postfix(Projectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched Projectile OnPool(LanceProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<LanceProjectile>();
                if (ModuleCheck != null)
                {
                    Collider fetchedCollider = (Collider)collodo.GetValue(__instance);
                    fetchedCollider.isTrigger = true;// Make it not collide
                    ModuleCheck.project = __instance;
                    Debug.Log("RandomAdditions: Overwrote Collision");
                }
                var pHP = __instance.gameObject.GetComponent<ProjectileHealth>();
                if (!(bool)pHP)
                {
                    pHP = __instance.gameObject.AddComponent<ProjectileHealth>();
                    pHP.GetHealth();
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
                    Debug.Log("RandomAdditions: Overwrote Mass - This enables physics collisions and will make the projectile more scary.");
                }
                /*
                var ModuleCheckI = __instance.gameObject.GetComponent<InterceptProjectile>();
                if (ModuleCheckI != null)
                {   // Handle intercept
                    __instance.gameObject.layer = Globals.inst.layerTank;
                }*/
            }
        }

        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("OnSpawn")]//On Creation
        private class PatchProjectileSpawn
        {
            private static void Prefix(Projectile __instance)
            {
                ProjectileManager.Add(__instance);
            }
        }

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("OnRecycle")]
        private class PatchProjectileRemove
        {
            private static void Prefix(Projectile __instance)
            {
                ProjectileManager.Remove(__instance);
            }
        }
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("Destroy")]
        private class PatchProjectileRemove2
        {
            private static void Prefix(Projectile __instance)
            {
                ProjectileManager.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("HandleCollision")]//On direct hit
        private class PatchProjectileCollision
        {
            static FieldInfo death = typeof(Projectile).GetField("m_LifeTime", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Prefix(Projectile __instance, ref Damageable damageable, ref Collider otherCollider)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
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
                if ((bool)damageable && ModuleCheck != null && (ManNetwork.IsHost || !ManNetwork.IsNetworked))
                {
                    var validation = damageable.GetComponent<TankBlock>();
                    if (!validation)
                    {
                        //Debug.Log("RandomAdditions: did not hit possible block");
                        return;
                    }
                    if (!ModuleCheck.InstaKill)
                        return;

                    if (__instance.Shooter.IsFriendly(validation.tank.Team) || validation.tank.IsNeutral())
                        return;// Stop friendly-fire

                    //Debug.Log("RandomAdditions: queued block death");
                    try
                    {
                        ModuleDeathInsurance.TryQueueUnstoppableDeath(validation);
                        Debug.Log("RandomAdditions: omae wa - mou shindeiru");
                        return;
                    }
                    catch
                    {
                        Debug.Log("RandomAdditions: Error on applying ModuleDeathInsurance!");
                    }
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
            private static void Postfix(Projectile __instance)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
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
            private static void Postfix(Projectile __instance, ref FireData fireData, ref Tank shooter)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                //Debug.Log("RandomAdditions: Patched Projectile Fire(WeightedProjectile)");
                var Split = __instance.GetComponent<SpiltProjectile>();
                if ((bool)Split)
                {
                    Split.Reset(__instance);
                }

                var ModuleCheckI = __instance.GetComponent<InterceptProjectile>();
                if (ModuleCheckI != null)
                {
                    var pd = fireData.GetComponent<ModulePointDefense>();
                    if ((bool)pd)
                        ModuleCheckI.Reset(pd.Target, pd.CanInterceptFast);
                    else
                        ModuleCheckI.Reset();
                }
                var ModuleCheck = __instance.GetComponent<WeightedProjectile>();
                if (ModuleCheck != null)
                {
                    if (shooter != null && ModuleCheck.CustomGravity && ModuleCheck.CustomGravityFractionSpeed)
                    {
                        Vector3 final = ((__instance.rbody.velocity - shooter.rbody.velocity) * ModuleCheck.GravityAndSpeedScale) + shooter.rbody.velocity;
                        Debug.Log("RandomAdditions: Scaled WeightedProjectile Speed from " + __instance.rbody.velocity + " to " + final);
                        __instance.rbody.velocity = final;
                    }
                }
                var ModuleCheck2 = __instance.GetComponent<TorpedoProjectile>();
                if (ModuleCheck2 != null)
                {
                    ModuleCheck2.OnFire();
                }
                var ModuleCheck3 = __instance.GetComponent<ProjectileHealth>();
                var ModuleCheck4 = __instance.GetComponent<LaserProjectile>();
                if (ModuleCheck3 != null)
                {
                    if (ProjectileHealth.IsFast(fireData.m_MuzzleVelocity) && !(bool)ModuleCheck4)
                    {
                        ProjectileManager.Add(__instance);
                        //ModuleCheck3.GetHealth(true);
                    }
                    else
                    {
                        //Debug.Log("RandomAdditions: ASSERT - Abberation in Projectile!  " + __instance.gameObject.name);
                        UnityEngine.Object.Destroy(ModuleCheck3);
                    }
                }
                else
                {
                    if (ProjectileHealth.IsFast(fireData.m_MuzzleVelocity) && !(bool)ModuleCheck4)
                    {
                        ProjectileManager.Add(__instance);
                        //ModuleCheck3.GetHealth(true);
                    }
                }
            }
        }

        
        [HarmonyPatch(typeof(Projectile))]
        [HarmonyPatch("SpawnExplosion")]//On Fire
        private class PatchProjectileForSplit
        {
            private static bool Prefix(Projectile __instance, ref Vector3 explodePos, ref Damageable directHitTarget)//ref Vector3 hitPoint, ref Tank Shooter, ref ModuleWeapon m_Weapon, ref int m_Damage, ref ManDamage.DamageType m_DamageType
            {
                var Split = __instance.GetComponent<SpiltProjectile>();
                if ((bool)Split)
                {
                    Split.OnExplosion();
                }
                try // Handle ModuleReinforced
                {
                    var ModuleCheck = __instance.GetComponent<OHKOProjectile>();
                    if (!(bool)directHitTarget || ModuleCheck)
                        return true;
                    var modifPresent = directHitTarget.GetComponent<ModuleReinforced>();
                    if ((bool)modifPresent)
                    {
                        if (modifPresent.DenyExplosion)
                        {   // Prevent explosion from triggering
                            Transform explodo = (Transform)InterceptProjectile.explode.GetValue(__instance);
                            if ((bool)explodo)
                            {
                                var boom = explodo.GetComponent<Explosion>();
                                if ((bool)boom)
                                {
                                    Explosion boom2 = explodo.Spawn(Singleton.dynamicContainer, explodePos).GetComponent<Explosion>();
                                    if ((bool)boom2)
                                    {
                                        boom2.m_EffectRadius = 2;
                                        boom2.m_EffectRadiusMaxStrength = 1;
                                    }
                                }
                            }
                            return false;
                        }
                    }
                }
                catch { }
                return true;
            }
        }
        

        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("OnPool")]
        private class PatchPooling
        {
            private static void Postfix(SeekingProjectile __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<InterceptProjectile>();
                if (ModuleCheck != null)
                {
                    ModuleCheck.GrabValues();
                }
            }
        }

        //Allow ignoring of lock-on
        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("GetManualTarget")]
        private class PatchLockOn
        {
            private static bool Prefix(SeekingProjectile __instance, ref Visible __result)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<SeekingProjectileIgnoreLock>();
                if (ModuleCheck != null)
                {
                    __result = null;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SeekingProjectile))]
        [HarmonyPatch("FixedUpdate")]
        private class PatchHomingForIntercept
        {
            private static bool Prefix(SeekingProjectile __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<DistractedProjectile>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.Distracted(__instance))
                        return false;
                }
                var ModuleCheck2 = __instance.gameObject.GetComponent<InterceptProjectile>();
                if (ModuleCheck2 != null)
                {
                    if (ModuleCheck2.Aiming)
                    {
                        if (ModuleCheck2.OverrideAiming(__instance))
                            if (ModuleCheck2.ForcedAiming)
                                return false;
                        if (ModuleCheck2.OnlyDefend)
                            return false;
                    }
                }
                return true;
            }
        }

        //Add torpedo functionality
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("DeactivateBoosters")]
        private class PatchMissileProjectileEnd
        {
            private static void Postfix(MissileProjectile __instance)
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

        // make MissileProjectle obey KeepSeekingProjectile
        [HarmonyPatch(typeof(MissileProjectile))]
        [HarmonyPatch("OnDelayedDeathSet")]
        private class PatchMissileProjectileOnCollide
        {
            private static bool Prefix(MissileProjectile __instance)
            {
                //Debug.Log("RandomAdditions: Patched MissileProjectile OnDelayedDeathSet(KeepSeekingProjectile)");
                var ModuleCheck = __instance.gameObject.GetComponent<KeepSeekingProjectile>();
                if (ModuleCheck != null)
                {
                    if (ModuleCheck.KeepBoosting)
                        return false;
                }
                return true;
            }
        }


       //-----------------------------------------------------------------------------------------------
       //-----------------------------------------------------------------------------------------------
       // Both used for Custom Blocks and Projectiles

       // Allow blocks to have one special resistance
       [HarmonyPatch(typeof(Damageable))]
        [HarmonyPatch("Damage")]//On damage handling
        private class PatchDamageable
        {
            private static void Prefix(Damageable __instance, ref ManDamage.DamageInfo info)
            {
                //Debug.Log("RandomAdditions: Patched Damageable Damage(ModuleReinforced)");
                try
                {
                    var modifPresent = __instance.gameObject.GetComponent<ModuleReinforced>();
                    if (modifPresent != null)
                    {
                        if ((bool)info.Source)
                        {
                            if (modifPresent.ModifyAoEDamage && info.Source.GetComponent<Explosion>())
                            {
                                info.ApplyDamageMultiplier(modifPresent.ExplosionMultiplier);
                            }
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
                            /*
                            Debug.Log("RandomAdditions: !! NEW DAMAGE TYPE DETECTED !!   ALERT CODER!!!");
                            Debug.Log("RandomAdditions: Type " + info.DamageType.ToString());
                            Debug.Log("RandomAdditions: Update ModuleReinforced and also Patch!");
                            //THROW THE GAME!
                            LogHandler.ThrowWarning("!!NEW DAMAGE TYPE DETECTED!!   ALERT CODER!!!\nType " + info.DamageType.ToString() + "\nUpdate ModuleReinforced and also PatchBatch!");
                            */
                        }
                    }
                }
                catch { }
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

        [HarmonyPatch(typeof(BubbleShield))]
        [HarmonyPatch("OnSpawn")]//On spawn
        private class PatchShieldsToActuallyBeShieldTyping
        {
            private static void Postfix(BubbleShield __instance)
            {
                if (KickStart.TrueShields && __instance.Damageable.DamageableType == ManDamage.DamageableType.Standard)
                {
                    __instance.Damageable.DamageableType = ManDamage.DamageableType.Shield;
                    //Debug.Log("RandomAdditions: PatchShieldsToActuallyBeShieldTyping - Changed " + __instance.transform.root.name + " to actually be shield typing");
                }
            }
        }

        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // Game tweaks

        [HarmonyPatch(typeof(TankCamera))]
        [HarmonyPatch("SetCameraShake")]//On annoying camera shake
        private class GetOutOfHereCameraShakeShack
        {
            static FieldInfo shaker = typeof(TankCamera).GetField("m_CameraShakeTimeRemaining", BindingFlags.NonPublic | BindingFlags.Instance);
            private static bool Prefix(TankCamera __instance)
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

        [HarmonyPatch(typeof(PlayerFreeCamera))]
        [HarmonyPatch("Enable")]//EXTEND RANGE
        private class MORE_RANGE
        {
            static FieldInfo overr = typeof(PlayerFreeCamera).GetField("maxDistanceFromTank", BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo underr = typeof(PlayerFreeCamera).GetField("groundClearance", BindingFlags.NonPublic | BindingFlags.Instance);
            private static void Prefix(PlayerFreeCamera __instance)
            {
                overr.SetValue(__instance, 250f);    // max "safe" range - 2.5x vanilla
                underr.SetValue(__instance, -25f);   // cool fights underground - You can do this in Build Beam so it makes sense
                Debug.Log("RandomAdditions: EXTENDED Free cam range");
            }
        }


        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        //-----------------------------------------------------------------------------------------------
        // The NEW crash handler with useful mod-crash-related information

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
                //reportBox.Find("Description").gameObject.GetComponent<InputField>().

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
                    bugReport.text = "<b>Well F*bron. TerraTech has crashed.</b> \n<b>This is a MODDED GAME AND THE DEVS CAN'T FIX MODDED GAMES!</b>  \nTake note of all your unofficial mods and send the attached Bug Report (make sure your name isn't in it!) below in the Official TerraTech Discord, in #modding-unofficial.";

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
                    Debug.Log("RandomAdditions: FAILIURE ON FETCHING LOG!");
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
    }
}
