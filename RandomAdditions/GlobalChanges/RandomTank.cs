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
        private bool isRecycled = true;
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
            tank.AttachEvent.Subscribe(OnAttach);
            tank.DetachEvent.Subscribe(OnDetach);
            tank.TankRecycledEvent.Subscribe(OnRecycled);
            enabled = true;
            InsureSolverIterations();
            DebugRandAddi.Info(tank.name + " - init RandomTank with " + damagedBlocks.Count + " damaged blocks.");
        }
        public void OnRecycled(Tank tank)
        {
            if (tank == this.tank)
            {
                //DebugRandAddi.Log(tank.name + " - OnRecycled RandomTank");
                CheckDamaged();
                //InvokeHelper.InvokeSingle(CheckDamaged, 0.1f);
                tank.TankRecycledEvent.Unsubscribe(OnRecycled);
                isRecycled = true;
            }
        }

        public void CheckDamaged()
        {
            try
            {
                foreach (var block in tank.blockman.IterateBlocks())
                {
                    OnAttach(block, tank);
                }
            }
            catch { }
        }
        public void OnDamaged(ManDamage.DamageInfo dmg, TankBlock damagedBlock)
        {
            if (dmg.Damage > 0)
            {
                if (damagedBlocks.Add(damagedBlock))
                {
                    //DebugRandAddi.Log(tank.name + " - New damaged block " + damagedBlock.name);
                }
            }
        }
        public void OnAttach(TankBlock newBlock, Tank tank)
        {
            if (isRecycled)
            {
                isRecycled = false;
                //DebugRandAddi.Log(tank.name + " - OnFirstAttach RandomTank");
                tank.TankRecycledEvent.Subscribe(OnRecycled);
                InvokeHelper.InvokeSingle(CheckDamaged, 0f);
            }
            if (!newBlock.visible.damageable.IsAtFullHealth)
            {
                if (!damagedBlocks.Contains(newBlock))
                {
                    //DebugRandAddi.Log(tank.name + " - Is new damaged block!");
                    damagedBlocks.Add(newBlock);
                }
            }
            //if (newBlock)
            //    DebugRandAddi.Log(tank.name + " - OnAttach called for " + newBlock.name);

        }
        public void OnDetach(TankBlock damagedBlock, Tank tank)
        {
            if (damagedBlocks.Contains(damagedBlock))
                damagedBlocks.Remove(damagedBlock);
            /*
            var fastener = GetComponent<ModuleFasteningLink>();
            if (fastener)
                ModulePatches.DetachingFrame.Remove(fastener);
            */
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
                //DebugRandAddi.Log(tank.name + " - There are approx " + damagedBlocks.Count + " damaged blocks left");
            }
        }

        public void OnAnchor(ModuleAnchor anchor, bool anchored, bool force)
        {
            ReevaluateLoadingDiameter();
            InsureSolverIterations();
        }

        public void UpdateColliderToggle()
        {
            foreach (var item in tank.blockman.IterateBlocks())
            {
                var CS = item.visible.ColliderSwapper;
                if (CS)
                {
                    CS.EnableCollision(!KickStart.ColliderDisable2);
                }
            }
        }
        public void InsureSolverIterations()
        {
            if (!Optimax.optimize)
                return;
            var rbody = GetComponent<Rigidbody>();
            if (rbody && rbody.solverIterations != Optimax.ColTankIterations)
            {
                //DebugRandAddi.Log("Rbody for tank altered - [" + rbody.solverIterations + " -> " + Optimax.ColTankIterations + "], [" + rbody.solverVelocityIterations + " -> "+ Optimax.ColTankIterations + "]");
                rbody.solverIterations = Optimax.ColTankIterations;
                rbody.solverVelocityIterations = Optimax.VelTankIterations;
            }
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
                OnClientTileLoadingToggle(true);
            }
            else if (MaxTileLoadingDiameter > 0)
                OnClientTileLoadingToggle(true);
            else
                OnClientTileLoadingToggle(false);
        }
        public void OnClientTileLoadingToggle(bool set)
        {
            if (set != isLoading)
            {
                if (set)
                {
                    ManWorldTileExt.ClientRegisterDynamicTileLoader(this);
                }
                else
                {
                    ManWorldTileExt.ClientUnregisterDynamicTileLoader(this);
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
