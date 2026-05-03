using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD;
using HarmonyLib;
using TerraTechETCUtil;
using UnityEngine;
using static CompoundExpression.EEInstance;

namespace RandomAdditions
{
    /// <summary>
    /// <b>WIP</b>
    /// <para>
    /// Built on the assumption most blocks obey their block bounds.
    /// Disabling visual meshes only has a small impact on game performance compared to C&S and Physics
    /// </para>
    /// <para>Note most of this is useless for concave hulls</para>
    /// </summary>
    public class GraphicsPhysicsCulling
    {
        //public static HashSet<BlockTypes> ReportedOn = new HashSet<BlockTypes>();
        public static uint HideObscurityDepth = 1;
        public static uint NoCollisionDepth = 1;

        private static HashSet<BlockRenderBounds> Pending = new HashSet<BlockRenderBounds>();
        private static bool AllShown = true;
        private static HashSet<TankBlock> ObscurersTemp = new HashSet<TankBlock>();
        //private static Stopwatch BlockHidingCalc = new Stopwatch();
        internal static void UpdateVisibility()
        {
            SetVisibilityOnALLTechs(AllShown);
        }
        public static void UpdateCulling()
        {
            if (KickStart.OcculsionCulling)
            {
                if (Input.GetKeyDown(KeyCode.L))
                {
                    AllShown = !AllShown;
                    UpdateVisibility();
                    ManSFX.inst.PlayMiscSFX(ManSFX.MiscSfxType.CheatCode);
                }
            }
            else if (!AllShown)
            {
                AllShown = true;
                UpdateVisibility();
            }
            if (Pending.Count > 0)
            {
                try
                {
                    if (KickStart.OcculsionCulling)
                    {
                        //BlockHidingCalc.Restart();
                        foreach (BlockRenderBounds bounds in Pending)
                        {
                            if (bounds?.block?.tank != null)
                            {
                                bounds.IsObscured = bounds.GetObscuritySLOW();
                                bounds.ObscuredLayerDepth = (uint)(bounds.IsObscured ? 1 : 0);
                            }
                        }
                        int obscuredCount = 0;
                        foreach (BlockRenderBounds bounds in Pending)
                        {
                            if (bounds?.block?.tank != null && bounds.IsObscured)
                                obscuredCount++;
                        }
                        if (obscuredCount > 0)
                        {
                            //DebugRandAddi.Log("Found " + obscuredCount + " obscured blocks");

                            int ObscureDepth = 0;
                            bool ObscuredAtLeastOnce = true;
                            while (ObscuredAtLeastOnce)
                            {
                                ObscuredAtLeastOnce = false;
                                foreach (BlockRenderBounds bounds in Pending)
                                {
                                    try
                                    {
                                        if (bounds?.block?.tank != null && bounds.IsObscured &&
                                            bounds.ObscuredLayerDepth >= ObscureDepth)
                                        {
                                            bool obscurerIsOutside = false;
                                            uint deepestDepth = uint.MaxValue;
                                            foreach (var bounds2 in bounds.IterateAllObscurersSLOW())
                                                ObscurersTemp.Add(bounds2);
                                            if (ObscurersTemp.Count > 0)
                                            {
                                                //DebugRandAddi.Log("Found " + ObscurersTemp.Count + " adjacent obscured blocks");
                                                foreach (var bounds2 in ObscurersTemp)
                                                {
                                                    var BRB = bounds2.GetComponent<BlockRenderBounds>();
                                                    uint depth = BRB.ObscuredLayerDepth;
                                                    if (!BRB.IsObscured)
                                                    {
                                                        obscurerIsOutside = true;
                                                        break;
                                                    }
                                                    if (deepestDepth > depth)
                                                        deepestDepth = depth;
                                                }
                                                if (obscurerIsOutside)
                                                {
                                                    //DebugRandAddi.Log("Obscured block is too close to outside!");
                                                    continue;
                                                }
                                                ObscuredAtLeastOnce = true;
                                                bounds.ObscuredLayerDepth = deepestDepth + 1;
                                            }
                                            else
                                            {
                                                //DebugRandAddi.Log("Found no adjacent obscured blocks");
                                                bounds.ObscuredLayerDepth = 1;
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        ObscurersTemp.Clear();
                                    }
                                }
                                ObscureDepth++;
                            }
                        }
                        else
                        {
                            //DebugRandAddi.Log("Found no obscured blocks");
                        }
                        /*
                        BlockHidingCalc.Stop();
                        DebugRandAddi.Log(nameof(GraphicsPhysicsCulling) + ".UpdateCulling() recalc " + Pending.Count(x => x?.block?.tank != null) +
                            " in " + BlockHidingCalc.ElapsedMilliseconds.ToString() + "ms");//*/
                        foreach (BlockRenderBounds bounds in Pending)
                        {
                            if (bounds?.block?.tank != null)
                            {
                                bounds.SetMeshColliderStateBasedOnDepth();
                                /*
                                if (bounds.ObscuredLayerDepth > 0)
                                    DebugRandAddi.Log("Assigned " + bounds.name + " depth of " + bounds.ObscuredLayerDepth);//*/
                            }
                        }
                    }
                    Pending.Clear();
                }
                catch (Exception e)
                {
                    //BlockHidingCalc.Stop();
                    Pending.Clear();
                    ManModGUI.ShowErrorPopup(e.ToString(), true);
                    throw e;
                }
            }
        }


        public static void SetVisibilityOnALLTechs(bool show)
        {
            foreach (var item in ManTechs.inst.IterateTechs())
            {
                if (item != null)
                    VisibilityAllOnTech(item, show);
            }
        }
        public static void VisibilityAllOnTech(Tank tank, bool show)
        {
            try
            {
                foreach (var item in tank.GetComponentsInChildren<BlockRenderBounds>())
                {
                    if (show)
                    {
                        item.SetColliderState(true);
                        item.SetMeshVisibilityState(true);
                    }
                    else
                        item.SetMeshColliderStateBasedOnDepth();
                }
            }
            catch { }
        }

        public class BlockRenderBounds : MonoBehaviour
        {
            private static HashSet<MeshRenderer> _MeshesToNOTHide = new HashSet<MeshRenderer>();

            internal TankBlock block = null;
            public bool AllSideAPOrArmor = false;
            public List<MeshRenderer> MeshesToHide = new List<MeshRenderer>();
            public bool HasIgnoredMeshes = false;
            public bool HasIgnoredColliders = false;
            public BoundsInt BlockBoundsAttached = default;
            public bool IsObscured = false;
            public uint ObscuredLayerDepth = 0;

            public static void PoolInit(TankBlock target)
            {
                BlockRenderBounds BRB = target.gameObject.AddComponent<BlockRenderBounds>();
                BRB.block = target;
                BRB.HasIgnoredColliders = BRB.DetermineDoNotDisableColliders(target);
                bool hasIgnoredMeshes = BRB.GetAllHideables();
                BRB.DetermineCanObscure(target, hasIgnoredMeshes);
                target.AttachedEvent.Subscribe(BRB.PostAttachInit);
                target.NeighbourAttachedEvent.Subscribe(BRB.NeighborsAddUpdate);
                target.NeighbourDetachedEvent.Subscribe(BRB.NeighborsRemoveUpdate);
                /*
                if (ReportedOn.Add((BlockTypes)target.GetComponent<Visible>().ItemType))
                    DebugRandAddi.Log("BlockRenderBounds added " + target.name + " - AllSideAPOrArmor: " + BRB.AllSideAPOrArmor.ToString());//*/
            }
            private void NeighborsAddUpdate(TankBlock neighboorDelta)
            {
                Pending.Add(this);
            }
            private void NeighborsRemoveUpdate(TankBlock neighboorDelta)
            {
                if (block.tank != null && ManSaveGame.Storing || ManTechSwapper.inst.CheckOperatingOnTech(block.tank))
                    return;// The tech is getting erased anyways
                Pending.Add(this);
                /*
                foreach (var neighbor in IterateAllObscurersSLOW())
                    Pending.Add(neighbor.GetComponent<BlockRenderBounds>());//*/
            }
            private void PostAttachInit()
            {
                Vector3 locPos = block.cachedLocalPosition;
                Vector3Int Pos = Vector3Int.RoundToInt(locPos);
                OrthoRotation Rot = block.cachedLocalRotation;
                BlockBoundsAttached = new BoundsInt(Vector3Int.RoundToInt(block.CalcFirstFilledCellLocalPos()), Vector3Int.zero);
                foreach (var cell in block.filledCells)
                {
                    Vector3Int vecPos = Pos + (Vector3Int)(Rot * cell);
                    BlockBoundsAttached.SetMinMax(new Vector3Int(
                        Mathf.Min(BlockBoundsAttached.xMin, vecPos.x),
                        Mathf.Min(BlockBoundsAttached.yMin, vecPos.y),
                        Mathf.Min(BlockBoundsAttached.zMin, vecPos.z)),
                        new Vector3Int(
                        Mathf.Max(BlockBoundsAttached.xMax, vecPos.x),
                        Mathf.Max(BlockBoundsAttached.yMax, vecPos.y),
                        Mathf.Max(BlockBoundsAttached.zMax, vecPos.z)));
                }
                IsObscured = false;
                ObscuredLayerDepth = 0;
                Pending.Add(this);
            }


            private bool DetermineDoNotDisableColliders(TankBlock target)
            {
                return target.GetComponent<ModuleWheels>() || target.GetComponent<ModuleShieldGenerator>()
                    || target.GetComponent<ModuleCircuit_AdaptiveGeometry>() || target.GetComponent<ModuleCircuit_Actuator_Door>()
                    || target.GetComponent<ModuleCircuit_Actuator_Ramp>();
            }

            /// <summary> </summary>
            /// <returns>true if block has ignored meshes</returns>
            private bool GetAllHideables()
            {
                try
                {
                    foreach (var m in gameObject.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        try
                        {
                            if (m.GetComponentsInChildren<MonoBehaviour>().Length > 0 || 
                                m.gameObject.layer == Globals.inst.layerWheelSuspension)
                                _MeshesToNOTHide.Add(m);
                        }
                        catch
                        {
                            _MeshesToNOTHide.Add(m);
                        }
                    }
                    foreach (var m in gameObject.GetComponentsInChildren<MeshRenderer>(false))
                    {
                        try
                        {
                            bool valid = true;
                            foreach (var m2 in m.GetComponentsInChildren<MeshRenderer>())
                            {
                                if (_MeshesToNOTHide.Contains(m2))
                                {
                                    valid = false;
                                    break;
                                }
                            }
                            if (valid)
                                MeshesToHide.Add(m);
                        }
                        catch { }
                    }
                    return _MeshesToNOTHide.Count > 0;
                }
                finally
                {
                    _MeshesToNOTHide.Clear();
                }
            }
            private void DetermineCanObscure(TankBlock target, bool hasIgnoredMeshes)
            {
                if (MeshesToHide.Count <= 1)
                {   // structural block, might be able to hide 
                    AllSideAPOrArmor = !hasIgnoredMeshes;
                }
                AllSideAPOrArmor = false;
            }

            public bool GetObscuritySLOW()
            {
                if (block.tank == null)
                    throw new InvalidOperationException("Tank is NULL");
                var TS = block.tank.blockman.GetTableCacheForPlacementCollection();
                TankBlock[,,] blockTableRaw = TS.blockTable;
                Vector3Int blockTableOffset = TS.blockTableCentre;
                /*
                DebugRandAddi.Log("GetObscuritySLOW for block bounds " + (BlockBoundsAttached.min + blockTableOffset).ToString() +
                    ", "+ (BlockBoundsAttached.max + blockTableOffset).ToString() + 
                    " where offset is " + blockTableOffset + " grid " + TS.size);
                Vector3Int OurBlockPosFirst = Vector3Int.RoundToInt(block.CalcFirstFilledCellLocalPos() + blockTableOffset);
                if (blockTableRaw[OurBlockPosFirst.x, OurBlockPosFirst.y, OurBlockPosFirst.z] != block)
                    throw new InvalidOperationException("Stupid block mismatch");//*/
                return GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(0, 0, 1)) && 
                    GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(0, 0, -1)) &&
                    GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(0, 1, 0)) && 
                    GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(0, -1, 0)) &&
                    GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(1, 0, 0)) && 
                    GetAxisObscurity(blockTableRaw, blockTableOffset, TS.size, new Vector3Int(-1, 0, 0));
            }
            private bool GetAxisObscurity(TankBlock[,,] blockTableRaw, Vector3Int blockTableOffset, int maxExts, Vector3Int direction)
            {
                bool obscured = false;
                if (direction.x == 1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 1, 2, 0, true);
                else if (direction.x == -1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 1, 2, 0, false);
                if (direction.y == 1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 0, 2, 1, true);
                else if (direction.y == -1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 0, 2, 1, false);
                if (direction.z == 1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 0, 1, 2, true);
                else if (direction.z == -1)
                    obscured |= BlockOrthographicObstructed(blockTableRaw, blockTableOffset, maxExts, 0, 1, 2, false);
                //DebugRandAddi.Log("GetAxisObscurity " + direction.ToString() + " obscured: " + obscured);
                return obscured;
            }
            private bool BlockOrthographicObstructed(TankBlock[,,] blockTableRaw, Vector3Int blockTableOffset, int maxExts, int axis1, int axis2, int outAxis, bool posOut)
            {
                int outAxisI;
                if (posOut)
                {
                    outAxisI = BlockBoundsAttached.max[outAxis] + blockTableOffset[outAxis] + 1;
                    if (outAxisI >= maxExts)
                    {
                        //DebugRandAddi.Log("BlockOrthographicObstructed ended range pos");
                        return false;
                    }
                }
                else
                {
                    outAxisI = BlockBoundsAttached.min[outAxis] + blockTableOffset[outAxis] - 1;
                    if (outAxisI < 0)
                    {
                        //DebugRandAddi.Log("BlockOrthographicObstructed ended range neg");
                        return false;
                    }
                }
                int axis2I = Mathf.Min(BlockBoundsAttached.max[axis2] + blockTableOffset[axis2] + 1, maxExts);
                int yI = Mathf.Max(BlockBoundsAttached.min[axis2] + blockTableOffset[axis2] - 1, 0);
                int axis1I = Mathf.Min(BlockBoundsAttached.max[axis1] + blockTableOffset[axis1] + 1, maxExts);
                for (int x = Mathf.Max(BlockBoundsAttached.min[axis1] + blockTableOffset[axis1] - 1, 0); x <= axis1I; x++)
                {
                    for (int y = yI; y <= axis2I; y++)
                    {
                        bool hit = false;
                        if (posOut)
                        {
                            for (int z = outAxisI; z < maxExts; z++)
                            {
                                try
                                {
                                    TankBlock blockGet = blockTableRaw[
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z)];
                                    /*
                                    Vector3 vec = new Vector3(
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z));
                                    DebugRandAddi.Log("Check at [" + vec.x + "," + vec.y + "," + vec.z + "] = block? " + (bool)blockGet + ", diff? " +  (blockGet != null && blockGet != block));
                                    //*/
                                    if (blockGet != null && blockGet != block)
                                    {
                                        hit = true;
                                        break;
                                    }
                                }
                                catch (IndexOutOfRangeException e)
                                {
                                    Vector3 vec = new Vector3(
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z));
                                    //ManModGUI.ShowErrorPopup("Out of range [" + vec.x + "," + vec.y + "," + vec.z + "] vs range " + maxExts + e.ToString(), true);
                                    throw e;
                                }
                            }
                        }
                        else
                        {
                            for (int z = outAxisI; z >= 0; z--)
                            {
                                try
                                {
                                    TankBlock blockGet = blockTableRaw[
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z)];
                                    /*
                                    Vector3 vec = new Vector3(
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z));
                                    DebugRandAddi.Log("Check at [" + vec.x + "," + vec.y + "," + vec.z + "] = block? " + (bool)blockGet + ", diff? " + (blockGet != null && blockGet != block));
                                    //*/
                                    if (blockGet != null && blockGet != block)
                                    {
                                        hit = true;
                                        break;
                                    }
                                }
                                catch (IndexOutOfRangeException e)
                                {
                                    Vector3 vec = new Vector3(
                                        axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                        axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                        axis1 == 2 ? x : (axis2 == 2 ? y : z));
                                    //ManModGUI.ShowErrorPopup("Out of range [" + vec.x + "," + vec.y + "," + vec.z + "] vs range " + maxExts + e.ToString(), true);
                                    throw e;
                                }
                            }
                        }
                        if (!hit)
                        {
                            /*
                            Vector3 vec = new Vector3(
                                axis1 == 0 ? x : (axis2 == 0 ? y : 0),
                                axis1 == 1 ? x : (axis2 == 1 ? y : 0),
                                axis1 == 2 ? x : (axis2 == 2 ? y : 0));
                            DebugRandAddi.Log("Failed to hit on pass " + vec);//*/
                            return false;
                        }
                    }
                }
                //DebugRandAddi.Log("Side is completely obstructed");
                return true;
            }

            public IEnumerable<TankBlock> IterateAllObscurersSLOW()
            {
                var TS = block.tank.blockman.GetTableCacheForPlacementCollection();
                TankBlock[,,] blockTableRaw = TS.blockTable;
                Vector3Int blockTableOffset = TS.blockTableCentre;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 1, 2, 0, true))
                    yield return item;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 1, 2, 0, false))
                    yield return item;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 0, 2, 1, true))
                    yield return item;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 0, 2, 1, false))
                    yield return item;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 0, 1, 2, true))
                    yield return item;
                foreach (var item in GetObscurer(blockTableRaw, blockTableOffset, TS.size, 0, 1, 2, false))
                    yield return item;
            }
            private IEnumerable<TankBlock> IterateObscurers(TankBlock[,,] blockTableRaw, Vector3Int blockTableOffset, int maxExts, Vector3Int direction)
            {
                if (direction.x == 1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 1, 2, 0, true);
                else if (direction.x == -1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 1, 2, 0, false);
                if (direction.y == 1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 0, 2, 1, true);
                else if (direction.y == -1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 0, 2, 1, false);
                if (direction.z == 1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 0, 1, 2, true);
                else if (direction.z == -1)
                    return GetObscurer(blockTableRaw, blockTableOffset, maxExts, 0, 1, 2, false);
                return null;
            }
            private IEnumerable<TankBlock> GetObscurer(TankBlock[,,] blockTableRaw, Vector3Int blockTableOffset, int maxExts, int axis1, int axis2, int outAxis, bool posOut)
            {
                int outAxisI;
                if (posOut)
                {
                    outAxisI = BlockBoundsAttached.max[outAxis] + blockTableOffset[outAxis] + 1;
                    if (outAxisI >= maxExts)
                        yield break;
                }
                else
                {
                    outAxisI = BlockBoundsAttached.min[outAxis] + blockTableOffset[outAxis] - 1;
                    if (outAxisI < 0)
                        yield break;
                }
                int axis2I = Mathf.Min(BlockBoundsAttached.max[axis2] + blockTableOffset[axis2] + 1, maxExts);
                int yI = Mathf.Max(BlockBoundsAttached.min[axis2] + blockTableOffset[axis2] - 1, 0);
                int axis1I = Mathf.Min(BlockBoundsAttached.max[axis1] + blockTableOffset[axis1] + 1, maxExts);
                for (int x = Mathf.Max(BlockBoundsAttached.min[axis1] + blockTableOffset[axis1] - 1, 0); x <= axis1I; x++)
                {
                    for (int y = yI; y <= axis2I; y++)
                    {
                        if (posOut)
                        {
                            for (int z = outAxisI; z < maxExts; z++)
                            {
                                TankBlock blockGet = blockTableRaw[
                                    axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                    axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                    axis1 == 2 ? x : (axis2 == 2 ? y : z)];
                                if (blockGet != null && blockGet != block)
                                {
                                    yield return blockGet;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (int z = outAxisI; z >= 0; z--)
                            {
                                TankBlock blockGet = blockTableRaw[
                                    axis1 == 0 ? x : (axis2 == 0 ? y : z),
                                    axis1 == 1 ? x : (axis2 == 1 ? y : z),
                                    axis1 == 2 ? x : (axis2 == 2 ? y : z)];
                                if (blockGet != null && blockGet != block)
                                {
                                    yield return blockGet;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            public void SetMeshVisibilityState(bool show)
            {
                foreach (var item in MeshesToHide)
                    item.enabled = show;
            }
            public void SetColliderState(bool show)
            {
                if (!HasIgnoredColliders)
                    block.GetComponent<ColliderSwapper>().EnableCollision(show);
            }


            public void SetMeshColliderStateBasedOnDepth()
            {
                SetMeshVisibilityState(AllShown || ObscuredLayerDepth <= HideObscurityDepth);
                SetColliderState(AllShown || ObscuredLayerDepth <= NoCollisionDepth);
            }
        }
    }
}
