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
        private const float MaxUnsubmergeSpeed = 0;

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
            RandomTank.AddBuoy(tank, this);
        }
        public override void OnDetach()
        {
            //DebugRandAddi.Log("OnDetach - ModuleBuoy");
            RandomTank.RemoveBuoy(tank, this);
        }


        public void GetCellsLocal(List<Vector3> cells)
        {
            cells.AddRange(cellsCache);
        }

        public bool ShouldFloat()
        {
            return block.trans.position.y - lowestPossiblePoint <= KickStart.WaterHeight;
        }
        public void GetFloatForceWorld(out Vector3 positionWorld, out float upForce)
        {
            float submergedRating = 0;
            Vector3 worldFloatCenter;
            if (CubeFloater)
            {   // we get lazy and process very roughly based on block extents
                float HeightAboveWater = block.trans.position.y - KickStart.WaterHeight;
                float rad = block.BlockCellBounds.extents.y * 0.866f;
                if (HeightAboveWater > rad)
                {
                    // We are above water, no effect
                }
                else if (HeightAboveWater > -rad)
                {
                    submergedRating += rad - (HeightAboveWater * cellsCache.Count);
                }
                else
                {
                    submergedRating += 1.732f * cellsCache.Count;
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
                        if (HeightAboveWater > 0.866f)
                        {
                            // We are above water, no effect
                        }
                        else if (HeightAboveWater >= -0.866f)
                        {
                            submergedRating += 0.866f - HeightAboveWater;
                        }
                        else
                        {
                            submergedRating += 1.732f;
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
                        if (HeightAboveWater > 0.866f)
                        {
                            // We are above water, no effect
                        }
                        else if (HeightAboveWater >= -0.866f)
                        {
                            submergedRating += 0.866f - HeightAboveWater;
                        }
                        else
                        {
                            submergedRating += 1.732f;
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
}
