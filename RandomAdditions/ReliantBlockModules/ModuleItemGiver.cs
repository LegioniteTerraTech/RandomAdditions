using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TerraTechETCUtil;

public class ModuleItemGiver : RandomAdditions.ModuleItemGiver { }
namespace RandomAdditions
{
    public class ModuleItemGiver : Module
    {
        private const float GiveTargetCheckDelay = 1.5f;

        private TankBlock TankBlock;
        private ModuleItemHolder itemHold;

        private Transform checkTrans;

        public float GiveCheckDistance = 5;
        public int GiveAmount = 1;

        private bool Disabled = false;
        private float LastCheckTime = 0;
        private ModuleItemStore TargetItemHolder = null;

        public void OnPool()
        {
            TankBlock = gameObject.GetComponent<TankBlock>();
            TankBlock.SubToBlockAttachConnected(OnAttach, OnDetach);

            itemHold = gameObject.GetComponent<ModuleItemHolder>();
            if (itemHold.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemGiver NEEDS a ModuleItemHolder to work!  This cannot be fixed automatically.\n  Cause of error - Block " + TankBlock.name);
                block.damage.SelfDestruct(0.1f);
                return;
            }

            checkTrans = KickStart.HeavyObjectSearch(transform, "_giveForwards");
            if (checkTrans.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: _giveForwards NOT SET!!!  Defaulting to base transform!\n  Cause of error - Block " + TankBlock.name);
                checkTrans = transform;
            }
        }
        private void OnAttach()
        {
            TankBlock.tank.Holders.HBEvent.Subscribe(OnHeartbeat);

            //ExtUsageHint.ShowExistingHint(4005);
        }
        private void OnDetach()
        {
            TankBlock.tank.Holders.HBEvent.Unsubscribe(OnHeartbeat);
        }

        private void OnHeartbeat(int HartC, TechHolders.Heartbeat HartStep)
        {
            if (HartStep == TechHolders.Heartbeat.PrePass)
            {
                var firstStack = itemHold.GetStack(0);
                if (!firstStack.IsEmpty)
                {
                    UpdateGiveTarget();
                    if (TargetItemHolder)
                    {
                        int step = 0;
                        var holder = TargetItemHolder.GetComponent<ModuleItemHolder>();
                        foreach (var item in holder.Stacks)
                        {
                            for (; !item.IsFull; step++)
                            {
                                if (firstStack.IsEmpty || step >= GiveAmount)
                                    return;
                                firstStack.Release(firstStack.FirstItem, item);
                            }
                        }
                    }
                }
            }
            if (HartStep == TechHolders.Heartbeat.PostPass)
            {
            }
        }

        private void UpdateGiveTarget()
        {
            if (Disabled)
            {
                TargetItemHolder = null;
            }
            else
            {
                if (LastCheckTime < Time.time)
                {
                    LastCheckTime = GiveTargetCheckDelay + Time.time;
                    if (Physics.Raycast(checkTrans.position, checkTrans.forward, out RaycastHit raycastHit, 1,
                        Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) && raycastHit.collider)
                    {
                        var vis = ManVisible.inst.FindVisible(raycastHit.collider);
                        if (vis)
                        {
                            var store = vis.GetComponent<ModuleItemStore>();
                            if (store && !vis.GetComponent<ModuleItemGiver>())
                            {
                                TargetItemHolder = store;
                                return;
                            }
                        }
                    }
                    TargetItemHolder = null;
                }
            }
        }
    }
}
