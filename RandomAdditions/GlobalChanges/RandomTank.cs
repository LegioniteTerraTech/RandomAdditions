using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Controls some of the basics of a Tech added by this mod
    /// </summary>
    public class RandomTank : MonoBehaviour, ITileLoader
    {
        //This handles the GUI clock used on the Tank.  Know your time and set your mines
        //  Charge your tech with solars before nightfall.
        private Tank tank;
        //private ClockManager man;
        public bool DisplayTimeTank = false;
        private readonly List<ModuleTileLoader> loaders = new List<ModuleTileLoader>();
        private bool isLoading = false;
        public int MaxTileLoadingDiameter = 1; // Only supports up to diamater of 5 for performance's sake

        // Water floating
        private bool buoysDirty = false;
        private readonly List<ModuleBuoy> buoys = new List<ModuleBuoy>();
        private readonly List<Vector3> localCells = new List<Vector3>();
        private float lowestPoint = 0;

        public static RandomTank Insure(Tank tank)
        {
            var rt = tank.GetComponent<RandomTank>();
            if (!rt)
            {
                rt = tank.gameObject.AddComponent<RandomTank>();
                rt.Initiate();
            }
            return rt;
        }
        public void Initiate()
        {
            tank = gameObject.GetComponent<Tank>();
            GlobalClock.tanks.Add(this);
            tank.AnchorEvent.Subscribe(OnAnchor);
            enabled = true;
        }

        public void OnAnchor(ModuleAnchor anchor, bool anchored, bool force)
        {
            ReevaluateLoadingDiameter();
        }

        internal void ResetUIValid()
        {
            DisplayTimeTank = false;
        }

        public static void AddTileLoader(Tank tank, ModuleTileLoader loader)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: RandomTank(HandleAddition) - TANK IS NULL");
                return;
            }
            var dis = Insure(tank);
            if (!dis.loaders.Contains(loader))
            {
                dis.loaders.Add(loader);
                dis.ReevaluateLoadingDiameter();
            }
            else
                DebugRandAddi.Log("RandomAdditions: RandomTank - ModuleTileLoader of " + tank.name + " was already added to " + tank.name + " but an add request was given?!?");
        }
        public static void RemoveTileLoader(Tank tank, ModuleTileLoader loader)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: RandomTank(HandleRemoval) - TANK IS NULL");
                return;
            }

            var dis = Insure(tank);

            if (dis.loaders.Remove(loader))
                dis.ReevaluateLoadingDiameter();
        }



        public static void AddBuoy(Tank tech, ModuleBuoy buoy)
        {
            RandomTank RT = Insure(tech);
            if (RT.buoys.Contains(buoy))
                DebugRandAddi.Assert("Buoy was ALREADY present in RandomTank");
            else
                RT.buoys.Add(buoy);
            RT.buoysDirty = true;
        }
        public static void RemoveBuoy(Tank tech, ModuleBuoy buoy)
        {
            RandomTank RT = Insure(tech);
            if (!RT.buoys.Remove(buoy))
                DebugRandAddi.Assert("Buoy was not present in RandomTank and was not removed");
            RT.buoysDirty = true;
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
            if (buoysDirty)
            {
                ReCalcBuoyencyThresholds();
                buoysDirty = false;
            }
            if (buoys.Count > 0)
                ApplyFloatForces();
        }

        private List<ModuleBuoy> floatingBuoys = new List<ModuleBuoy>();
        public void ApplyFloatForces()
        {
            if (KickStart.isWaterModPresent && tank.rbody)
            {
                float upwardForceStrength = 0;
                Vector3 forceCenter = Vector3.zero;

                floatingBuoys.Clear();
                foreach (var item in buoys)
                {
                    if (item.ShouldFloat())
                        floatingBuoys.Add(item);
                }
                foreach (var item in floatingBuoys)
                {
                    item.GetFloatForceWorld(out Vector3 posWorld, out float addForce);
                    forceCenter += posWorld;
                    upwardForceStrength += addForce;
                }
                if (floatingBuoys.Count > 0)
                    tank.rbody.AddForceAtPosition(Vector3.up * upwardForceStrength, forceCenter / floatingBuoys.Count, ForceMode.Force);
            }
        }


        // TileLoaders
        public void ReevaluateLoadingDiameter()
        {
            MaxTileLoadingDiameter = 0;
            foreach (var item in loaders)
            {
                if (MaxTileLoadingDiameter < item.MaxTileLoadingDiameter
                    && !(item.AnchorOnly && !tank.IsAnchored))
                    MaxTileLoadingDiameter = item.MaxTileLoadingDiameter;
            }
            if (tank.PlayerFocused)
            {
                MaxTileLoadingDiameter = 2;
                TileLoadingToggle(true);
            }
            else if (MaxTileLoadingDiameter > 0)
                TileLoadingToggle(true);
            else
                TileLoadingToggle(false);
        }
        public void TileLoadingToggle(bool set)
        {
            if (set != isLoading)
            {
                if (set)
                {
                    ManTileLoader.RegisterDynamicTileLoader(this);
                }
                else
                {
                    ManTileLoader.UnregisterDynamicTileLoader(this);
                }
            }
        }

        public IntVector2 GetCenterTile()
        { 
            return WorldPosition.FromScenePosition(tank.visible.centrePosition).TileCoord;
        }
        public List<IntVector2> GetActiveTiles()
        {
            if (!tank || !ManSpawn.IsPlayerTeam(tank.Team))
                return new List<IntVector2>();

            List<IntVector2> tiles;
            IntVector2 centerTile = GetCenterTile();
            int radCentered;
            Vector2 posTechCentre;
            Vector2 posTileCentre;
            switch (MaxTileLoadingDiameter)
            {
                case 0:
                    return new List<IntVector2>();
                case 1:
                    tiles = new List<IntVector2> { centerTile };
                    break;
                case 2:
                    posTechCentre = tank.boundsCentreWorld.ToVector2XZ();
                    posTileCentre = ManWorld.inst.TileManager.CalcTileOriginScene(centerTile).ToVector2XZ();
                    if (posTechCentre.x > posTileCentre.x)
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            tiles = new List<IntVector2> { centerTile,
                            centerTile + new IntVector2(1,0),
                            centerTile + new IntVector2(1,1),
                            centerTile + new IntVector2(0,1),
                            };
                        }
                        else
                        {
                            tiles = new List<IntVector2> { centerTile,
                            centerTile + new IntVector2(1,0),
                            centerTile + new IntVector2(1,-1),
                            centerTile + new IntVector2(0,-1),
                            };
                        }
                    }
                    else
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            tiles = new List<IntVector2> { centerTile,
                            centerTile + new IntVector2(-1,0),
                            centerTile + new IntVector2(-1,1),
                            centerTile + new IntVector2(0,1),
                            };
                        }
                        else
                        {
                            tiles = new List<IntVector2> { centerTile,
                            centerTile + new IntVector2(-1,0),
                            centerTile + new IntVector2(-1,-1),
                            centerTile + new IntVector2(0,-1),
                            };
                        }
                    }
                    break;
                case 3:
                    tiles = new List<IntVector2>();
                    radCentered = 1;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            tiles.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    break;
                case 4:
                    tiles = new List<IntVector2>();
                    radCentered = 1;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            tiles.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    posTechCentre = tank.boundsCentreWorld.ToVector2XZ();
                    posTileCentre = ManWorld.inst.TileManager.CalcTileOriginScene(centerTile).ToVector2XZ();
                    if (posTechCentre.x > posTileCentre.x)
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            tiles = new List<IntVector2> {
                            centerTile + new IntVector2(2,-1),
                            centerTile + new IntVector2(2,0),
                            centerTile + new IntVector2(2,1),
                            centerTile + new IntVector2(2,2),
                            centerTile + new IntVector2(1,2),
                            centerTile + new IntVector2(0,2),
                            centerTile + new IntVector2(-1,2),
                            };
                        }
                        else
                        {
                            tiles = new List<IntVector2> {
                            centerTile + new IntVector2(2,1),
                            centerTile + new IntVector2(2,0),
                            centerTile + new IntVector2(2,-1),
                            centerTile + new IntVector2(2,-2),
                            centerTile + new IntVector2(1,-2),
                            centerTile + new IntVector2(0,-2),
                            centerTile + new IntVector2(-1,-2),
                            };
                        }
                    }
                    else
                    {
                        if (posTechCentre.y > posTileCentre.y)
                        {
                            tiles = new List<IntVector2> {
                            centerTile + new IntVector2(-2,-1),
                            centerTile + new IntVector2(-2,0),
                            centerTile + new IntVector2(-2,1),
                            centerTile + new IntVector2(-2,2),
                            centerTile + new IntVector2(-1,2),
                            centerTile + new IntVector2(0,2),
                            centerTile + new IntVector2(1,2),
                            };
                        }
                        else
                        {
                            tiles = new List<IntVector2> {
                            centerTile + new IntVector2(-2,1),
                            centerTile + new IntVector2(-2,0),
                            centerTile + new IntVector2(-2,-1),
                            centerTile + new IntVector2(-2,-2),
                            centerTile + new IntVector2(-1,-2),
                            centerTile + new IntVector2(0,-2),
                            centerTile + new IntVector2(1,-2),
                            };
                        }
                    }
                    break;
                default:
                    tiles = new List<IntVector2>();
                    radCentered = 2;
                    for (int step = -radCentered; step <= radCentered; step++)
                    {
                        for (int step2 = -radCentered; step2 <= radCentered; step2++)
                        {
                            tiles.Add(centerTile + new IntVector2(step, step2));
                        }
                    }
                    break;
            }
            return tiles;
        }

    }
}
