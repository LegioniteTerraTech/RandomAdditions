using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RandomAdditions.Minimap;
using TerraTechETCUtil;
using UnityEngine;
using UnityEngine.UI;

namespace RandomAdditions
{
    internal static class Patches
    {
        // ------------------  BUG FIXES  ------------------

        [HarmonyPatch(typeof(ManVisible))]
        [HarmonyPatch("UnregisterColliderToVisibleLookup")]//
        [HarmonyPriority(-9001)]
        internal static class StopRemovalOfNonExistantColliders
        {
            private static Dictionary<Collider, Visible> EmergencyLookup = null;
            internal static bool Prefix(ManVisible __instance, Collider c,
               ref Dictionary<Collider, Visible> ___m_ColliderToVisibleLookup)
            {
                try
                {
                    if (___m_ColliderToVisibleLookup == null)
                    {
                        DebugRandAddi.Assert("ManVisible.m_ColliderToVisibleLookup is NULL??? - MAKING NEW");
                        ___m_ColliderToVisibleLookup = new Dictionary<Collider, Visible>();
                    }
                    if (c?.gameObject?.name == null)
                    {
                        DebugRandAddi.Assert("Collider is NULL??? Running a sanity check NOW");

                        if (EmergencyLookup == null)
                            EmergencyLookup = new Dictionary<Collider, Visible>();
                        foreach (var item in ___m_ColliderToVisibleLookup)
                        {
                            if (item.Key != null && item.Key != null)
                                EmergencyLookup.Add(item.Key, item.Value);
                            else
                                DebugRandAddi.Log(" - INVALID: " + (item.Key?.name == null ? "<NULL>" : item.Key.name) + ", " +
                                    (item.Value?.name == null ? "<NULL>" : item.Value.name));
                        }
                        if (___m_ColliderToVisibleLookup.Count != EmergencyLookup.Count)
                            DebugRandAddi.Log("Removed " + (___m_ColliderToVisibleLookup.Count - EmergencyLookup.Count) + " invalid entries");
                        var temp = ___m_ColliderToVisibleLookup;
                        temp.Clear();
                        ___m_ColliderToVisibleLookup = EmergencyLookup;
                        EmergencyLookup = temp;
                        return false;
                    }
                }
                catch
                {   // Crashed in there somehow - the best course of action is to ABORT
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TechDataAvailValidation))]
        [HarmonyPatch("RecordBlockData")]//
        [HarmonyPriority(-9001)]
        internal static class TrySaveTechs
        {
            private static TechData weDid;
            internal static void Postfix(TechDataAvailValidation __instance, ref TechData tech,
               Dictionary<BlockTypes, TechDataAvailValidation.BlockTypeAvailability> ___m_BlockAvailability)
            {
                if (KickStart.TrySaveMyTechs && weDid != tech)
                {
                    weDid = tech;
                    bool saveMe = false;
                    foreach (var item in ___m_BlockAvailability)
                    {
                        if (item.Value.availability == TechDataAvailValidation.BlockAvailableState.NotAvailableInGame)
                        {
                            saveMe = true;
                            break;
                        }
                    }
                    if (saveMe)
                    {
                        if (TechRescuer.TryRescue(tech))
                            __instance.RecordBlockData(tech);// relog AGAIN  
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BlockManager))]
        [HarmonyPatch("CleanupInvalidTechBlocks")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkFix
        {
            private static MethodInfo _CleanupInvalidBlockOnTech = AccessTools.Method(typeof(BlockManager), "CleanupInvalidBlockOnTech",
                new Type[] { typeof(TankBlock), typeof(TechSplitNamer), typeof(bool) });
            private static MethodInfo _FixupAfterRemovingBlocks = AccessTools.Method(typeof(BlockManager), "FixupAfterRemovingBlocks");
            private static List<TankBlock> Temp = new List<TankBlock>();
            internal static bool Prefix(BlockManager __instance, Tank ___tank, List<TankBlock> ___allBlocks)
            {
                bool isAnchored = ___tank.IsAnchored;
                TechSplitNamer techSplitNamer = null;
                bool flag;
                do
                {
                    flag = false;
                    try
                    {
                        Temp.Clear();
                        foreach (TankBlock tankBlock in ___allBlocks)
                        {
                            if (tankBlock.NumConnectedAPs == 0 || !tankBlock.CanReachRoot())
                                Temp.Add(tankBlock);
                        }
                        int removeCount = Temp.Count;
                        foreach (var tankBlock in Temp)
                        {   // Changes it so that it "gives up" on illegal blocks I guess. Better than hanging
                            _CleanupInvalidBlockOnTech.Invoke(__instance, new object[] { tankBlock, techSplitNamer, isAnchored });
                            int removeCount2 = 0;
                            foreach (var item in ___allBlocks)
                            {
                                if (item.NumConnectedAPs == 0 || !item.CanReachRoot())
                                    removeCount2++;
                            }
                            if (removeCount2 != removeCount)
                                flag = true; // removed.
                        }
                    }
                    catch { }
                }
                while (flag);
                Temp.Clear();
                _FixupAfterRemovingBlocks.Invoke(__instance, new object[] { false });
                return false;
            }
        }

        public static OrthoRotation SetCorrectRotation(Quaternion changeRot)
        {
            Vector3 foA = (changeRot * Vector3.forward).normalized;
            Vector3 upA = (changeRot * Vector3.up).normalized;
            //Debug.Log("Architech: SetCorrectRotation - Matching test " + foA + " | " + upA);
            Quaternion qRot2 = Quaternion.LookRotation(foA, upA);
            OrthoRotation rot = new OrthoRotation(qRot2);
            if (rot != qRot2)
            {
                bool worked = false;
                for (int step = 0; step < OrthoRotation.NumDistinctRotations; step++)
                {
                    OrthoRotation rotT = new OrthoRotation(OrthoRotation.AllRotations[step]);
                    bool isForeMatch = (rotT * Vector3.forward).Approximately(foA, 0.35f);
                    bool isUpMatch = (rotT * Vector3.up).Approximately(upA, 0.35f);
                    if (isForeMatch && isUpMatch)
                    {
                        rot = rotT;
                        worked = true;
                        break;
                    }
                }
                if (!worked)
                {
                    DebugRandAddi.Log("RandomAdditions: SetCorrectRotation - Matching failed - OrthoRotation is missing edge case " + foA + " | " + upA);
                }
            }
            return rot;
        }

        [HarmonyPatch(typeof(ModuleFasteningLink))]
        [HarmonyPatch("ReplacePartsWithWhole")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkFix2
        {
            internal static bool Prefix(ModuleFasteningLink __instance, ModuleFasteningLink counterpart,
               ModuleFasteningLink.LinkParts[] ___m_DetachedBlocks, BlockTypes ___m_CombinedBlock)
            {
                TankBlock[] blocksToRemove = new TankBlock[]
                {
                    __instance.block,
                    counterpart.block
                };
                TankPreset.BlockSpec[] array = new TankPreset.BlockSpec[1];
                ModuleFasteningLink.LinkParts linkParts = ___m_DetachedBlocks[0];
                Vector3 cachedLocalPosition = __instance.block.cachedLocalPosition;
                OrthoRotation cachedLocalRotation = __instance.block.cachedLocalRotation;
                byte skinIndex = __instance.block.GetSkinIndex();
                Quaternion rotation = cachedLocalRotation * Quaternion.Inverse(Quaternion.Euler(linkParts.localRot));
                array[0] = new TankPreset.BlockSpec
                {
                    m_BlockType = ___m_CombinedBlock,
                    position = cachedLocalPosition + cachedLocalRotation * linkParts.localPos,
                    orthoRotation = SetCorrectRotation(rotation),
                    m_SkinID = skinIndex
                };
                Singleton.Manager<ManLooseBlocks>.inst.HostReplaceBlock(blocksToRemove, array);
                return false;
            }
        }
        [HarmonyPatch(typeof(ModuleFasteningLink))]
        [HarmonyPatch("AddBlocksToTech")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkFix3
        {
            internal static bool Prefix(ModuleFasteningLink __instance, Tank sourceT, Tank destT,
                TankBlock sourceBlock, TankBlock destBlock)
            {
                //Quaternion rotationCorrected = Quaternion.Inverse(sourceT.trans.rotation) * destT.trans.rotation;
                Vector3 cachedLocalPosition = sourceBlock.cachedLocalPosition;
                Vector3 cachedLocalPosition2 = destBlock.cachedLocalPosition;
                //cachedLocalPosition + destBlock.cachedLocalRotation * this.m_DetachedBlocks[1].localPos;
                KeyValuePair<TankBlock, TankPreset.BlockSpec>[] array =
                    new KeyValuePair<TankBlock, TankPreset.BlockSpec>[sourceT.blockman.blockCount];
                int num = 0;
                foreach (TankBlock tankBlock in sourceT.blockman.IterateBlocks())
                {
                    TankPreset.BlockSpec item = default;
                    item.InitFromBlockState(tankBlock, true);
                    Vector3 a = sourceT.trans.position + sourceT.trans.rotation * tankBlock.cachedLocalPosition;
                    Vector3 v = Quaternion.Inverse(destT.trans.rotation) * (a - destT.trans.position);
                    item.position = v;
                    Quaternion rhs = sourceT.trans.rotation * tankBlock.cachedLocalRotation;
                    item.orthoRotation = SetCorrectRotation((Quaternion.Inverse(destT.trans.rotation) * rhs).AlignToAxis());
                    array[num++] = new KeyValuePair<TankBlock, TankPreset.BlockSpec>(tankBlock, item);
                }
                sourceT.blockman.Disintegrate(false, false);
                using (ManSpawn.PopulateTechHelper populateTechHelper = new ManSpawn.PopulateTechHelper(destT, false, false, null, false, false, true, true, null))
                {
                    foreach (KeyValuePair<TankBlock, TankPreset.BlockSpec> valueTuple in array)
                    {
                        TankBlock item2 = valueTuple.Key;
                        TankPreset.BlockSpec item3 = valueTuple.Value;
                        populateTechHelper.AddBlock(item2, item3, false, null);
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(ModuleFasteningLink))]
        [HarmonyPatch("Unlink")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkFix4
        {
            internal static bool Prefix(ModuleFasteningLink __instance, BlockTypes ___m_CombinedBlock,
               ModuleFasteningLink.LinkParts[] ___m_DetachedBlocks)
            {
                if (!__instance.block.IsAttached || __instance.block.BlockType != ___m_CombinedBlock)
                    return false;
                TankPreset.BlockSpec[] array = new TankPreset.BlockSpec[___m_DetachedBlocks.Length];
                Vector3 cachedLocalPosition = __instance.block.cachedLocalPosition;
                OrthoRotation cachedLocalRotation = __instance.block.cachedLocalRotation;
                byte skinIndex = __instance.block.GetSkinIndex();
                for (int i = 0; i < ___m_DetachedBlocks.Length; i++)
                {
                    Quaternion rotation = cachedLocalRotation * Quaternion.Euler(___m_DetachedBlocks[i].localRot);
                    array[i] = new TankPreset.BlockSpec
                    {
                        m_BlockType = ___m_DetachedBlocks[i].type,
                        position = cachedLocalPosition + cachedLocalRotation * ___m_DetachedBlocks[i].localPos,
                        orthoRotation = SetCorrectRotation(rotation),
                        m_SkinID = skinIndex
                    };
                }
                Singleton.Manager<ManLooseBlocks>.inst.HostReplaceBlock(new TankBlock[]
                {
                    __instance.block
                }, array);
                return false;
            }
        }
        static float tempLinkTime = 0;
        [HarmonyPatch(typeof(ModuleFasteningLink))]
        [HarmonyPatch("TryLink")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkSpeedControl
        {
            internal static void Prefix(ModuleFasteningLink __instance, ref float ___m_LinkLerpDuration)
            {
                if (tempLinkTime == 0)
                    tempLinkTime = ___m_LinkLerpDuration;
                if (KickStart.FastenerSpeed != 0)
                    ___m_LinkLerpDuration = tempLinkTime / ((10f + KickStart.FastenerSpeed) / 10f);
                else
                    ___m_LinkLerpDuration = tempLinkTime;
            }
        }
        [HarmonyPatch(typeof(ModuleFasteningLink))]
        [HarmonyPatch("ContinuouslyTryLinkNearby")]//
        [HarmonyPriority(-9001)]
        internal static class ModuleFasteningLinkSpeedControl2
        {
            internal static void Prefix(ModuleFasteningLink __instance, ref float ___m_LinkLerpDuration)
            {
                if (tempLinkTime == 0)
                    tempLinkTime = ___m_LinkLerpDuration;
                if (KickStart.FastenerSpeed != 0)
                    ___m_LinkLerpDuration = tempLinkTime / ((10f + KickStart.FastenerSpeed) / 10f);
                else
                    ___m_LinkLerpDuration = tempLinkTime;
            }
        }


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


        /// <summary>
        /// This solely exists because there exists edge-cases where the MaterialSwapper can get called on m_Renderers which have had one of their
        ///   listed renderers removed by inproperly setup blocks that have a render that removed AFTER they are spawned!  
        ///   May have a performance hit when handling C&S and I don't like that at all but better than a crash!
        /// </summary>
        [HarmonyPatch(typeof(MaterialSwapper))]
        [HarmonyPriority(9001)]
        [HarmonyPatch("SetMaterialPropertiesOnRenderers", new Type[] { typeof(float), typeof(float),
        typeof(Vector2),typeof(MaterialSwapper.VariableColorOverrides),typeof(List<Renderer>),})]
        private static class MaterialSwapperFix
        {
            internal static void Prefix(ref List<Renderer> renderers)
            {
                renderers.RemoveAll(renderer =>
                {
                    if (renderer == null)
                    {
                        DebugRandAddi.Assert("Null renderer in SetMaterialPropertiesOnRenderers call.\nWE SHOULD NOT BE LOOKING FOR THESE!!! - " +
                            StackTraceUtility.ExtractStackTrace());
                        return true;
                    }
                    return false;
                });
            }
        }



        // ------------------  PERFORMANCE  ------------------

        /* // Test to see if reducing block attachments reduces delayed lag on tech spawn - NO
        [HarmonyPatch(typeof(BlockManager))]
        [HarmonyPatch("AddBlockToTech")]//
        [HarmonyPriority(-9001)]
        internal static class TryFindForLessLag
        {
            static int failInterval = 0;
            internal static bool Prefix()
            {
                failInterval++;
                if (failInterval > 3)
                    failInterval = 0;
                return failInterval == 0;
            }
        }//*/
        /*  // Test to see if TileManager makes delayed lag on tech spawn - NO
        [HarmonyPatch(typeof(TileManager))]
        [HarmonyPatch("UpdateTileCache")]//
        [HarmonyPriority(-9001)]
        internal static class TileManagerForLessLag
        {
            internal static void Prefix(TileManager __instance, Visible visible)
            {
                DebugRandAddi.Log("TileManager.UpdateTileCache() nextUpdateTime " + visible.tileCache.nextUpdateTime);
            }
        }//*/
        [HarmonyPatch(typeof(Circuits))]
        [HarmonyPatch("DoCircuitLoop")]//
        [HarmonyPriority(-9001)]
        internal static class CircuitsForLessLag
        {
            internal static bool Prefix(Circuits __instance)
            {
                return !KickStart.noCircuits || ManNetwork.IsNetworked;
            }
        }
        /*  // Test to see if TechCircuits makes delayed lag on tech spawn - NO
        [HarmonyPatch(typeof(TechCircuits))]
        [HarmonyPatch("RebuildCircuitNetworksForDirtyConnexions")]//
        [HarmonyPriority(-9001)]
        internal static class CircuitsForLessLag
        {
            internal static bool Prefix(Circuits __instance)
            {
                return !KickStart.noCircuits || ManNetwork.IsNetworked;
            }
        }//*/

        /*
        public static int UpdateConnexionLinksCalls = 0;
        [HarmonyPatch(typeof(ModuleCircuitNode))]
        [HarmonyPatch("UpdateConnexionLinks")]//
        [HarmonyPriority(-9001)]
        internal static class DisableModuleCircuitsForFasterBuilding
        {
            internal static bool Prefix(ModuleCircuitNode __instance)
            {
                UpdateConnexionLinksCalls++;
                //DebugRandAddi.Log("UpdateConnexionLinks called for " + (__instance.name.NullOrEmpty() ? "<NULL>" : __instance.name));
                return true;
            }
        }//*/

        [HarmonyPatch(typeof(ManEOS))]
        [HarmonyPatch("DoFullLogin", new Type[] { typeof(string), typeof(string), typeof(Action), })]
        [HarmonyPriority(-9001)]
        internal static class ShutUpEOS
        {
            internal static bool Prefix(ManEOS __instance, Action onLogAttemptedCallback)
            {
                if (SKU.IsSteam && KickStart.IDontTrustEpicAtAll)
                {
                    DebugRandAddi.Log("RandomAdditions: ManEOS.DoFullLogin was called!  You don't trust them so we deny the send request");
                    if (onLogAttemptedCallback != null)
                        onLogAttemptedCallback();
                    return false;
                }
                return true;
            }
        }

        /*
        [HarmonyPatch(typeof(BlockManager))]
        [HarmonyPatch("SetTableSize")]//
        [HarmonyPriority(-9001)]
        internal static class TrackWhenWeRescaleBlockTable
        {
            public static Stopwatch watch = new Stopwatch();
            internal static void Prefix(BlockManager __instance, int newSize, TankBlock[,,] ___blockTable)
            {
                DebugRandAddi.Log("BlockManager.SetTableSize() Start for size " + newSize + 
                    " from size " + ___blockTable.GetLength(0));
                watch.Restart();
            }
            internal static void Postfix(BlockManager __instance, int newSize)
            {
                watch.Stop();
                DebugRandAddi.Log("BlockManager.SetTableSize() End at time " + watch.ElapsedMilliseconds + "ms");
            }
        }//*/





        // ------------------  NEW FEATURES  ------------------

        [HarmonyPatch(typeof(UIItemDisplay))]
        [HarmonyPatch("Setup", new Type[] { typeof(ItemTypeInfo), typeof(Color), typeof(Color),
        typeof(string), typeof(Color), typeof(bool), typeof(bool)})]//
        [HarmonyPriority(-9001)]
        internal static class AllowModWrenchIconRescale
        {
            private static Vector3 defaultScale = Vector3.zero;
            private static Vector2 defaultOffset = Vector2.zero;
            internal static void Prefix(UIItemDisplay __instance, Image ___m_ModdedItem)
            {
                if (___m_ModdedItem == null)
                    return;
                if (defaultScale == Vector3.zero)
                {
                    defaultScale = ___m_ModdedItem.transform.localScale;
                    //defaultOffset = ___m_ModdedItem.rectTransform.anchoredPosition;
                }
                ___m_ModdedItem.transform.localScale = defaultScale * KickStart.ModWrenchScale;
                //___m_ModdedItem.rectTransform.anchoredPosition = defaultOffset * KickStart.ModWrenchScale;
            }
        }


        [HarmonyPatch(typeof(UITechManagerEntry))]
        [HarmonyPatch("Init")]//
        internal static class GrabUIEntryDetails
        {
            internal class TrackedJumpState
            {
                public Button button;
                public TrackedVisible TV;
                public TooltipComponent TC;
                public Image img;
                public void UpdateJumpState()
                {
                    bool canJump = MinimapExtRandi.CanTeleportSafely(TV);
                    button.interactable = canJump;
                    if (canJump)
                    {
                        LOC_DirectControl.SetTextAuto(TC);
                        TC.SetMode(UITooltipOptions.Default);
                        img.color = mockColor;
                    }
                    else
                    {
                        LOC_DirectControlNotAvail.SetTextAuto(TC);
                        TC.SetMode(UITooltipOptions.Warning);
                        img.color = AltUI.ColorDefaultEnemy;
                    }
                }
            }
            private static List<TrackedJumpState> tracked = new List<TrackedJumpState>();
            internal static void SlowUpdateThis()
            {
                tracked.RemoveAll(x => x.button == null);
                foreach (var state in tracked)
                    state.UpdateJumpState();
            }

            const string name = "JumpTo_Button";
            static FieldInfo TargetInfo = typeof(UITechManagerEntry).GetField("m_TrackedVis", BindingFlags.Instance | BindingFlags.NonPublic);
            static Material mockIcon = null;
            static Sprite mockSprite = null;
            static Color mockColor = default;
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
                DebugRandAddi.Info("Creating new " + name);
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
                    //DebugRandAddi.Log(parent2.name + " posLoc: " + parent2.localPosition.ToString());
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
                GO.name = name;
                GO.transform.localPosition = new Vector3(Mathf.Lerp(244f, 454f, locatorIndex / 4f), 0.6f, 0.0f);//404.0f
                Button.ButtonClickedEvent BCE = new Button.ButtonClickedEvent();
                BCE.AddListener(() =>
                {
                    TrackedVisible TV = (TrackedVisible)TargetInfo.GetValue(__instance);
                    MinimapExtRandi.TryJumpPlayer(TV);
                    ManSFX.inst.PlayUISFX(ManSFX.UISfxType.Enter);
                });
                newButton.onClick = BCE;
                Image IHide = null;
                foreach (Image image in newImages)
                {
                    if (!image.name.Contains("Button"))
                    {
                        if (mockIcon == null)
                        {
                            mockColor = image.color;
                            mockIcon = new Material(newImages[0].material.shader);
                            mockSprite = UIHelpersExt.GetGUIIcon("HUD_Slider_Graphics_01_1");
                            mockIcon.mainTexture = mockSprite.texture;
                        }
                        image.material = mockIcon;
                        image.sprite = mockSprite;
                        CanvasRenderer CR = image.GetComponent<CanvasRenderer>();
                        CR.SetPopMaterial(mockIcon, 0);
                        CR.SetMaterial(mockIcon, 0);
                        CR.SetTexture(mockIcon.mainTexture);
                        image.SetAllDirty();
                        IHide = image;
                    }
                }
                TrackedJumpState TJS = new TrackedJumpState()
                {
                    button = newButton,
                    TC = newTooltip,
                    TV = tv,
                    img = IHide,
                };
                tracked.Add(TJS);
                // SETTING IMAGE DOES NOT WORK FOR SOME STUPID REASON
                //Utilities.LogGameObjectHierachy(newButton.gameObject);
                newButton.image.material = mockIcon;
                CanvasRenderer CR2 = newButton.image.GetComponent<CanvasRenderer>();
                CR2.SetPopMaterial(mockIcon, 0);
                CR2.SetMaterial(mockIcon, 0);
                newButton.image.SetAllDirty();
                DebugRandAddi.Info("Created new " + name);
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
        [HarmonyPatch("PlayOneshot", new Type[] { typeof(TechAudio.AudioTickData), typeof(FMODEvent.FMODParams) })]//
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





        // ------------------  NEW AUDIO FOR COMBAT THEMES AND ENGINES  ------------------

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
