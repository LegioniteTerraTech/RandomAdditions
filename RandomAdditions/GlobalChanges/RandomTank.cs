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

        public void Initiate()
        {
            tank = gameObject.GetComponent<Tank>();
            GlobalClock.tanks.Add(this);
            tank.AnchorEvent.Subscribe(OnAnchor);
        }

        public void OnAnchor(ModuleAnchor anchor, bool anchored, bool force)
        {
            ReevaluateLoadingDiameter();
        }

        internal void ResetUIValid()
        {
            DisplayTimeTank = false;
        }

        public static void HandleAddition(Tank tank, ModuleTileLoader loader)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: RandomTank(HandleAddition) - TANK IS NULL");
                return;
            }
            var dis = tank.GetComponent<RandomTank>();
            if (!dis.loaders.Contains(loader))
            {
                dis.loaders.Add(loader);
                dis.ReevaluateLoadingDiameter();
            }
            else
                DebugRandAddi.Log("RandomAdditions: RandomTank - ModuleTileLoader of " + tank.name + " was already added to " + tank.name + " but an add request was given?!?");
        }
        public static void HandleRemoval(Tank tank, ModuleTileLoader loader)
        {
            if (tank.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: RandomTank(HandleRemoval) - TANK IS NULL");
                return;
            }

            var dis = tank.GetComponent<RandomTank>();

            if (dis.loaders.Remove(loader))
                dis.ReevaluateLoadingDiameter();
        }

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

        public List<IntVector2> GetActiveTiles()
        {
            if (!ManSpawn.IsPlayerTeam(tank.Team))
                return new List<IntVector2>();

            List<IntVector2> tiles;
            IntVector2 centerTile = WorldPosition.FromScenePosition(tank.visible.centrePosition).TileCoord;
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
