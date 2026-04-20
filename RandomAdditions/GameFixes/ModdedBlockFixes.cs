using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD;
using HarmonyLib;
using MonoMod.Utils;
using TerraTechETCUtil;
using UnityEngine;

namespace RandomAdditions
{
    public static class ModdedBlockFixes
    {
        private static MethodInfo setIns = AccessTools.Method(typeof(ModuleCircuitNode), "ConfigureCircuitInputOnAllAPs");

        private static MethodInfo consumerFix = AccessTools.Property(typeof(TankBlock), "openMenuEventConsumer")?.GetSetMethod();

        /// <summary>
        /// ONLY DOES BASE LAYER
        /// </summary>
        private static void CopySettingsFromPrefabIfMissing<T>(TankBlock target, BlockTypes reference)
            where T : MonoBehaviour
        {
            var refer = ManSpawn.inst.GetBlockPrefab(reference); 
            if (refer == null)
            {
                ManModGUI.ShowErrorPopup("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() failed for " +
                    (target.name.NullOrEmpty() ? "<NULL>" : target.name) +
                    " since prefab (" + reference.ToString() + ") does not exists");
            }
            else if (target != null)
            {
                T refEx = refer.GetComponent<T>();
                if (!refEx)
                {
                    ManModGUI.ShowErrorPopup("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() failed for " +
                        (target.name.NullOrEmpty() ? "<NULL>" : target.name) +
                        " since prefab's (" + reference.ToString() + ") own component ref of type  does not exists");
                    return; // ref target is NULL!!!
                }
                T exists = target.GetComponent<T>();
                if (!exists)
                {  // Else we add new
                    exists = target.gameObject.AddComponent<T>();
                    try
                    {
                        foreach (var item in AccessTools.GetDeclaredFields(typeof(T)))
                            item.SetValue(exists, item.GetValue(refEx));
                    }
                    catch (Exception e)
                    {
                        ManModGUI.ShowErrorPopup("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() failed for " +
                            (target.name.NullOrEmpty() ? "<NULL>" : target.name) + " on stage 0 - " + e);
                    }
                    try
                    {   // try init it properly-ish fu-
                        AccessTools.Method(typeof(T), "OnPool")?.Invoke(exists, Array.Empty<object>());
                    }
                    catch (Exception e)
                    {
                        ManModGUI.ShowErrorPopup("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() failed for " +
                            (target.name.NullOrEmpty() ? "<NULL>" : target.name) + " on stage 1 - " + e);
                    }
                    try
                    {
                        if (consumerFix == null)
                            throw new NullReferenceException(nameof(consumerFix));
                        if (typeof(T).IsCompatible(typeof(ManPointer.OpenMenuEventConsumer)))
                            consumerFix.Invoke(target, new object[] { exists });
                    }
                    catch (Exception e)
                    {
                        ManModGUI.ShowErrorPopup("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() failed for " +
                            (target.name.NullOrEmpty() ? "<NULL>" : target.name) + " on stage 2 - " + e);
                    }
                    DebugRandAddi.Log("CopySettingsFromPrefabIfMissing<" + nameof(T) + ">() - fixed " + (target.name.NullOrEmpty() ? "<NULL>" : target.name));
                }
            }
        }

        private static void FixBlockContextMenu(TankBlock block)
        {
            if (block.HasContextMenu)
            {   // Try fix errored contextMenus
                switch (block.ContextMenuType)
                {
                    case ManHUD.HUDElementType._deprecated_HoverControl:
                        CopySettingsFromPrefabIfMissing<ModuleHoverControl>(block, BlockTypes.BF_Control_HoverPower_111);
                        break;
                    case ManHUD.HUDElementType._deprecated_MassControl:
                        CopySettingsFromPrefabIfMissing<ModuleHUDSliderControl>(block, BlockTypes.GC_Mass_Variable_222);
                        break;
                    case ManHUD.HUDElementType.BlockControlOnOff:
                    case ManHUD.HUDElementType._deprecated_CircuitWiFiFrequencyControl:
                    case ManHUD.HUDElementType._deprecated_CircuitAccumulatorControl:
                    case ManHUD.HUDElementType._deprecated_CircuitAmplifierControl:
                    case ManHUD.HUDElementType.SliderControlRadialMenu:
                    case ManHUD.HUDElementType.PowerToggleBlockMenu:
                    case ManHUD.HUDElementType.BlockOptionsContextMenu:
                    case ManHUD.HUDElementType.CircuitsNSystemsDebugger:
                    case ManHUD.HUDElementType.SimpleOnOffRadial:
                    default:
                        break;
                }
            }
        }
        private static void FixBlockCnS(TankBlock block)
        {
            // make all weapon APs C&S compatable
            var Vis = block.GetComponent<Visible>();
            if (Vis != null)
            {
                if (Vis.m_ItemType == null)
                    throw new NullReferenceException(nameof(Vis.m_ItemType));
                BlockTypes BT = (BlockTypes)Vis.ItemType;
                var BTS = BlockIndexer.GetBlockDetails(BT);
                if (BTS.IsWeapon)
                {
                    var node = block.GetComponent<ModuleCircuitNode>();
                    if (node != null)
                    {
                        var flagAlreadyChecked = (ModuleCircuitNode.AutoGenChargePoints)6;
                        if (!node.AutoGenChargePointsForAllAPs.HasFlag(flagAlreadyChecked))
                        {
                            if (ManMusicEnginesExt.ShouldAddCnSAPsWeapon((int)ManSpawn.inst.GetCorporation(BT)))
                            {
                                if (setIns == null)
                                    throw new NullReferenceException(nameof(setIns));
                                setIns.Invoke(node, Array.Empty<object>());
                                DebugRandAddi.Log("Added C&S APs for block " + (block.name.NullOrEmpty() ? "<NULL>" : block.name));
                            }
                            node.AutoGenChargePointsForAllAPs |= flagAlreadyChecked;
                        }
                    }
                }
            }
        }
        internal static void FixBlock(TankBlock block)
        {
            try
            {
                OptimizeOutline.FlagNonRendTrans(block.transform);
            }
            catch (Exception e)
            {
                throw new Exception("Failed in OptimizeOutline.FlagNonRendTrans()", e);
            }
            try
            {
                FixBlockContextMenu(block);
            }
            catch (Exception e)
            {
                throw new Exception("Failed while trying to fix context menus", e);
            }
            try
            {
                FixBlockCnS(block);
            }
            catch (Exception e)
            {
                throw new Exception("Failed when trying to make modded weapon APs C&S compatable", e);
            }
        }


    }
}
