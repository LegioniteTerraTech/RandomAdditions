using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleBuoy : RandomAdditions.ModuleBuoy { };
namespace RandomAdditions
{
    /// <summary>
    /// Make a block float in water.  Does not float on it's own.
    /// </summary>
    public class ModuleBuoy : ExtModule
    {
        public override BlockDetails.Flags BlockDetailFlags => BlockDetails.Flags.WaterFloats;
        private bool singleCell;
        private List<Vector3> cellsCache = new List<Vector3>();
        private float cellFloatForce = 0;
        internal float lowestPossiblePoint;

        public float FloatForce = 1;
        public bool ForcesFixedToCOM = true;
        public bool CubeFloater = false;

        protected override void Pool()
        {
            singleCell = block.filledCells.Count() < 2;
            if (singleCell)
            {
                cellsCache.Add(block.filledCells[0]);
                lowestPossiblePoint = 1.732f;
            }
            else
            {
                float furthestPointFromCenter = int.MaxValue;
                foreach (var item in block.filledCells)
                {
                    cellsCache.Add(block.trans.localPosition + (block.trans.localRotation * item));
                    float sqrMag = item.sqrMagnitude;
                    if (sqrMag < furthestPointFromCenter)
                    {
                        furthestPointFromCenter = sqrMag;
                    }
                }
                lowestPossiblePoint = Mathf.Sqrt(furthestPointFromCenter) + 0.866f;
            }
            cellFloatForce = (FloatForce / cellsCache.Count) / 1.732f;
        }

        public override void OnAttach()
        {
            //DebugRandAddi.Log("OnAttach - ModuleBuoy");
            TankBuoy.stat.HandleAddition(this);
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach - ModuleBuoy");
            TankBuoy.stat.HandleRemoval(this);
        }


        public void GetCellsLocal(List<Vector3> cells)
        {
            cells.AddRange(cellsCache);
        }

        public bool ShouldFloat()
        {
            return block.trans.position.y - lowestPossiblePoint <= KickStart.WaterHeight;
        }
        public void GetFloatForceWorldLAZY(out Vector3 positionWorld, out float upForce)
        {
            float submergedRating = 0;
            // we get lazy and process very roughly based on block extents
            float HeightAboveWater = block.trans.position.y - KickStart.WaterHeight;
            float rad = block.BlockCellBounds.extents.y * 0.866f;
            if (HeightAboveWater < -rad)
            {   // Subby
                submergedRating += 1.732f * cellsCache.Count;
            }
            else if (HeightAboveWater < rad)
            {   // Partially in Water
                submergedRating += rad - (HeightAboveWater * cellsCache.Count);
            }
            else
            {   // We are above water, no effect
            }
            upForce = cellFloatForce * submergedRating;
            positionWorld = block.centreOfMassWorld;
        }
        public void GetFloatForceWorld(out Vector3 positionWorld, out float upForce)
        {
            float submergedRating = 0;
            Vector3 worldFloatCenter;
            if (CubeFloater)
            {   // we get lazy and process very roughly based on block extents
                float HeightAboveWater = block.trans.position.y - KickStart.WaterHeight;
                float rad = block.BlockCellBounds.extents.y * 0.866f;
                if (HeightAboveWater < -rad)
                {   // Subby
                    submergedRating += 1.732f * cellsCache.Count;
                }
                else if (HeightAboveWater < rad)
                {   // Partially in Water
                    submergedRating += rad - (HeightAboveWater * cellsCache.Count);
                }
                else
                {   // We are above water, no effect
                }
                worldFloatCenter = block.centreOfMassWorld;
            }
            else
            {   // Use precise calculating
                if (ForcesFixedToCOM)
                {
                    foreach (var item in cellsCache)
                    {
                        float HeightAboveWater = block.trans.TransformPoint(item).y - KickStart.WaterHeight;
                        if (HeightAboveWater < -0.866f)
                        {   // Subby
                            submergedRating += 1.732f;
                        }
                        else if (HeightAboveWater < 0.866f)
                        {   // Partially in Water
                            submergedRating += 0.866f - HeightAboveWater;
                        }
                        else
                        {   // We are above water, no effect
                        }
                    }
                    worldFloatCenter = block.centreOfMassWorld;
                }
                else
                {
                    worldFloatCenter = Vector3.zero;
                    foreach (var item in cellsCache)
                    {
                        Vector3 cellCenterWorld = block.trans.TransformPoint(item);
                        worldFloatCenter += cellCenterWorld;
                        float HeightAboveWater = cellCenterWorld.y - KickStart.WaterHeight;
                        if (HeightAboveWater < -0.866f)
                        {   // Subby
                            submergedRating += 1.732f;
                        }
                        else if (HeightAboveWater < 0.866f)
                        {   // Partially in Water
                            submergedRating += 0.866f - HeightAboveWater;
                        }
                        else
                        {   // We are above water, no effect
                        }
                    }
                    worldFloatCenter /= cellsCache.Count;
                }
            }
            upForce = cellFloatForce * submergedRating;
            positionWorld = worldFloatCenter;
            DebugRandAddi.Assert(upForce > (FloatForce + 1), "Somehow, the float acceleration has bypassed the set amount: " + FloatForce + " vs " + upForce);
        }
    }
    public class TankBuoy : MonoBehaviour, ITankCompAuto<TankBuoy, ModuleBuoy>
    {
        public static ITankCompAuto<TankBuoy, ModuleBuoy> stat => null;
        public TankBuoy Inst => this;
        public Tank tank { get; set; }
        public HashSet<ModuleBuoy> Modules => buoys;

        private const float MaxUnsubmergeSpeed = 14;
        private const int MaxBlocksPreciseFloat = 32;

        // Water floating
        private bool buoysDirty = false;
        private readonly HashSet<ModuleBuoy> buoys = new HashSet<ModuleBuoy>();
        private readonly List<Vector3> localCells = new List<Vector3>();
        public float lowestPoint { get; private set; } = 0;


        public void StartManagingPre() { }
        public void StartManagingPost() { }
        public void StopManaging() { }
        public void AddModule(ModuleBuoy buoy)
        {
            buoysDirty = true;
        }
        public void RemoveModule(ModuleBuoy buoy)
        {
            buoysDirty = true;
        }


        public void ReCalcBuoyencyThresholds()
        {
            localCells.Clear();
            float furthest = 0;
            foreach (var item in buoys)
            {
                item.GetCellsLocal(localCells);
                float dist = (item.lowestPossiblePoint * item.lowestPossiblePoint) + item.transform.localPosition.sqrMagnitude;
                if (dist > furthest)
                {
                    furthest = dist;
                }
            }
            lowestPoint = Mathf.Sqrt(furthest) + 0.866f;
            //DebugRandAddi.Log("Recalced BuoyencyThresholds");
        }


        public void FixedUpdate()
        {
            if (!ManPauseGame.inst.IsPaused)
            {
                if (buoysDirty)
                {
                    ReCalcBuoyencyThresholds();
                    buoysDirty = false;
                }
                if (buoys.Count > 0)
                    ApplyFloatForces();
            }
        }

        private List<ModuleBuoy> floatingBuoys = new List<ModuleBuoy>();
        public void ApplyFloatForces()
        {
            if (KickStart.isWaterModPresent && tank.rbody)
            {
                floatingBuoys.Clear();
                foreach (var item in buoys)
                {
                    if (item.ShouldFloat())
                        floatingBuoys.Add(item);
                }
                if (floatingBuoys.Count > 0)
                {
                    float upwardForceStrength = 0;
                    Vector3 forceCenter = Vector3.zero;

                    if (floatingBuoys.Count > MaxBlocksPreciseFloat)
                    {   // Lazy floating to save performance!
                        foreach (var item in floatingBuoys)
                        {
                            item.GetFloatForceWorldLAZY(out Vector3 posWorld, out float addForce);
                            forceCenter += posWorld;
                            upwardForceStrength += addForce;
                        }
                    }
                    else
                    {
                        foreach (var item in floatingBuoys)
                        {
                            item.GetFloatForceWorld(out Vector3 posWorld, out float addForce);
                            forceCenter += posWorld;
                            upwardForceStrength += addForce;
                        }
                    }
                    if (tank.rbody.velocity.y < MaxUnsubmergeSpeed)
                    {
                        float curUpwardsForces = tank.rbody.mass * tank.rbody.velocity.y;
                        float MaxUpwardsForceForFloat = tank.rbody.mass * MaxUnsubmergeSpeed;
                        float appliedFloatForce = Mathf.Clamp(upwardForceStrength, 0, MaxUpwardsForceForFloat - curUpwardsForces);
                        if (appliedFloatForce > 0)
                        {
                            tank.rbody.AddForceAtPosition(Vector3.up * appliedFloatForce, forceCenter / floatingBuoys.Count, ForceMode.Force);
                        }
                    }
                }
            }
        }

    }
}
