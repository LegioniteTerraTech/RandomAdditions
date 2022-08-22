using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace RandomAdditions
{
    internal class ModulePatches : MassPatcher
    {
        internal static class ModuleItemHolderBeamPatches
        {
            internal static Type target = typeof(ModuleItemHolderBeam);
            //Allow disabling of physics on mobile bases
            /// <summary>
            /// PatchModuleItemHolderBeamForStatic
            /// </summary>
            private static bool UpdateFloat_Prefix(ModuleItemHolderBeam __instance, ref Visible item)
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
                        //DebugRandAddi.Log("RandomAdditions: Overwrote trac beams to remain on");
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

            //Allow disabling of physics on mobile bases
            /// <summary>
            /// PatchModuleItemHolderBeamForStatic2
            /// </summary>
            private static bool OnTechAnchored_Prefix(ModuleItemHolderBeam __instance)
            {
                var ModuleCheck = __instance.gameObject.GetComponent<ModuleItemFixedHolderBeam>();
                if (ModuleCheck != null)
                {
                    //DebugRandAddi.Log("RandomAdditions: Overwrote trac beams to remain on");
                    return false;
                }
                return true;
            }

            //Override and send operations over to ModuleItemFixedHolderBeam
            /// <summary>
            /// PatchModuleItemHolderBeamForStatic3
            /// </summary>
            private static bool SetPositionsInStack_Prefix(ModuleItemHolderBeam __instance)
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

        internal static class ModuleBoosterPatches
        {
            internal static Type target = typeof(BoosterJet);
            //Let BurnerJet do it's job accurately
            /// <summary>
            /// RunModuleBoosterBurner
            private static void OnFixedUpdate_Prefix(BoosterJet __instance)
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

        internal static class ModuleItemConsumePatches
        {
            internal static Type target = typeof(ModuleItemConsume);
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            //-----------------------------------------------------------------------------------------------
            // Try make crafting a bit more bearable

            //Return items on crafting cancelation
            /// <summary>
            /// PatchModuleItemConsumerToReturnInputs
            /// </summary>
            private static void CancelRecipe_Prefix(ModuleItemConsume __instance)
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

                            //DebugRandAddi.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                            int ToReturn = totRequest - notAvail;
                            //DebugRandAddi.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
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

                            //DebugRandAddi.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                            int ToReturn = totRequest - notAvail;
                            //DebugRandAddi.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
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
                    DebugRandAddi.Log("RandomAdditions: CONSUME - Nothing more to eject.");
                }
            }

            //Return items on fabgrab
            /// <summary>
            /// PatchModuleItemConsumerToReturnOnReset
            /// </summary>
            private static void ResetState_Prefix(ModuleItemConsume __instance)
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

                            //DebugRandAddi.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                            int ToReturn = totRequest - notAvail;
                            //DebugRandAddi.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
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

                            //DebugRandAddi.Log("RandomAdditions: CONSUME - still requesting " + notAvail + " chunks");
                            int ToReturn = totRequest - notAvail;
                            //DebugRandAddi.Log("RandomAdditions: CONSUME - returning " + ToReturn + " chunks");
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
                    //DebugRandAddi.Log("RandomAdditions: CONSUME INPUTS - Nothing more to eject.");
                }
                /* // Output emergency throwout -  doesn't work as the devs mess something up down the line
                 * //   And null something that they can still pull somehow on their end
                try
                {   // Output emergency throwout
                    //if ((int)isFinished.GetValue(__instance) == 1)
                    //{
                    //    DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - Queued");
                    //}
                    DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - Queued");
                    if (ejectorTransform != null)
                    {
                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - what");
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
                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - " + fireTimesOut + " items in reserve");
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
                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - 0");
                        Stack<ItemTypeInfo> reMatch = new Stack<ItemTypeInfo>();
                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - 0.5");

                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - 1");
                        int fireTimesOut;
                        try
                        {
                            int queuepre = fetchedRecipe.m_OutputItems.Length;
                            DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - count " + queuepre);
                            for (int Queue = queuepre - 1; Queue >= 0; Queue--)
                            {   // shove to the queue to shove out all at once

                                _ = reMatch.Count;
                                DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - 2");
                                RecipeTable.Recipe.ItemSpec itemSpec = fetchedRecipe.m_OutputItems[Queue];
                                for (int N = 0; N < itemSpec.m_Quantity; N++)
                                {
                                    DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout) - 3");
                                    reMatch.Push(itemSpec.m_Item);
                                }
                            }
                            fireTimesOut = reMatch.Count;
                        }
                        catch 
                        {
                            fireTimesOut = fetchedRecipeS.outputQueue.Count;
                        }
                        DebugRandAddi.Log("RandomAdditions: CONSUME (Output throwout - Fallback) - " + fireTimesOut + " items in reserve");
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

                    DebugRandAddi.Log("RandomAdditions: CONSUME OUTPUTS - " + e);
                    //DebugRandAddi.Log("RandomAdditions: CONSUME OUTPUTS - Nothing more to eject.");
                }
                */
            }
        }

        internal static class ModuleItemHolderPatches
        {
            internal static Type target = typeof(ModuleItemHolder.Stack);
            //Handle internal silo stacks and input fake values
            /// <summary>
            /// PatchModuleItemHolderStackToSeeInternalSiloContents
            /// </summary>
            private static void OfferAllItemsToCollector_Postfix(ModuleItemHolder.Stack __instance, ref ItemSearchCollector collector)
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
                                    //DebugRandAddi.Log("RandomAdditions: Added " + __instance.items[0].pickup.ChunkType.ToString() + " to available recipie items");
                                }
                                //DebugRandAddi.Log("RandomAdditions: Searched silo (Chunks)");
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
                                //DebugRandAddi.Log("RandomAdditions: Searched silo (Blocks)");
                            }
                            ModuleCheck.WasSearched = true;
                        }
                    }
                }
            }

        }
    }
}
