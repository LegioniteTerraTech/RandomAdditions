using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;
using RandomAdditions.RailSystem;

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

        // Health
        public bool Damaged => damagedBlocks.Count > 0;
        private HashSet<TankBlock> damagedBlocks = new HashSet<TankBlock>();

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
            tank.DetachEvent.Subscribe(OnDetach);
            enabled = true;
        }

        public void OnDamaged(ManDamage.DamageInfo dmg, TankBlock damagedBlock)
        {
            if (dmg.Damage > 0)
            {
                damagedBlocks.Add(damagedBlock);
            }
        }
        public void OnDetach(TankBlock damagedBlock, Tank tank)
        {
            if (damagedBlocks.Contains(damagedBlock))
                damagedBlocks.Remove(damagedBlock);
        }

        public void GetDamagedBlocks(List<TankBlock> toAddTo)
        {
            if (Damaged)
            {
                int stepper = 0;
                while (damagedBlocks.Count > stepper)
                {
                    var caseB = damagedBlocks.ElementAt(stepper);
                    if (caseB != null && caseB.tank == tank && caseB.visible.isActive && !caseB.visible.damageable.IsAtFullHealth)
                    {
                        toAddTo.Add(caseB);
                        stepper++;
                    }
                    else
                        damagedBlocks.Remove(caseB);
                }
            }
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
            if (tank.PlayerFocused || (GetComponent<TankLocomotive>() && GetComponent<TankLocomotive>().ShouldLoadTiles()))
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
                    ManWorldTileExt.RegisterDynamicTileLoader(this);
                }
                else
                {
                    ManWorldTileExt.UnregisterDynamicTileLoader(this);
                }
            }
        }

        public IntVector2 GetCenterTile()
        {
            return tank.visible.tileCache.tile.Coord;
        }
        public void GetActiveTiles(List<IntVector2> cache)
        {
            if (!tank || !ManSpawn.IsPlayerTeam(tank.Team))
                return;
            ManWorldTileExt.GetActiveTilesAround(cache,
                WorldPosition.FromScenePosition(tank.boundsCentreWorldNoCheck),
                MaxTileLoadingDiameter);
        }

    }
}
