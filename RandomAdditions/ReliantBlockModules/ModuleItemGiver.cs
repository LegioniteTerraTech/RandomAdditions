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
    public class ModuleItemGiver : ExtModule
    {
        private const float GiveTargetCheckDelay = 0.5f;

        private ModuleItemHolder itemHold;

        private Transform grabStack;
        private Transform checkTrans;
        private Transform canGiveTrans;
        private Transform targetAvailTrans;

        public float GiveCheckDistance = 5;
        public int GiveAmount = 1;
        public int NeighboorStackAPIndex = 0;

        private bool Disabled = false;
        private float LastCheckTime = 0;
        private ModuleItemHolder TargetItemHolder = null;
        private bool needsToDropFirst = false;
        private ModuleItemHolder.Stack pullStack = null;

        // Logic
        public int DisableAPIndex = 0;
        private bool LogicConnected = false;

        protected override void Pool()
        {
            itemHold = gameObject.GetComponent<ModuleItemHolder>();
            if (itemHold.IsNull())
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleItemGiver NEEDS a ModuleItemHolder to work!  This cannot be fixed automatically.\n  Cause of error - Block " + block.name);
                block.damage.SelfDestruct(0.1f);
                return;
            }
            if (block.attachPoints.Length <= NeighboorStackAPIndex || NeighboorStackAPIndex < 0)
            {
                LogHandler.ThrowWarning("RandomAdditions: ModuleItemGiver's NeighboorStackAPIndex is not within range [" + 0 + " - " + block.attachPoints.Length +
                    "]!  This cannot be fixed automatically.\n  Cause of error - Block " + block.name);
                block.damage.SelfDestruct(0.1f);
                return;
            }
            if (!itemHold.IsFlag(ModuleItemHolder.Flags.TakeFromSilo))
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemGiver NEEDS ModuleItemHolder set to take from silos (use a filter as a base reference)!  This cannot be fixed automatically.\n  Cause of error - Block " + block.name);
            }

            checkTrans = KickStart.HeavyTransformSearch(transform, "_giveForwards");
            if (checkTrans.IsNull())
            {
                DebugRandAddi.Log("RandomAdditions: ModuleItemGiver _giveForwards NOT SET!!!  Defaulting to base transform!\n  Cause of error - Block " + block.name);
                checkTrans = transform;
            }
            grabStack = KickStart.HeavyTransformSearch(transform, "_getStack");
            canGiveTrans = KickStart.HeavyTransformSearch(transform, "_giveCan");
            targetAvailTrans = KickStart.HeavyTransformSearch(transform, "_giveDo");

            itemHold.StackConnectEvent.Subscribe(OnStackConnect);
            itemHold.StackDisconnectEvent.Subscribe(OnStackDisconnect);
            itemHold.SetReleaseFilterCallback(OnReleaseFiltered);

            itemHold.OverrideStackCapacity(GiveAmount);
            UpdateArrows(0);
        }
        public override void OnAttach()
        {
            tank.Holders.HBEvent.Subscribe(OnHeartbeat);

            if (CircuitExt.LogicEnabled)
            {
                if (block.CircuitNode?.Receiver)
                {
                    LogicConnected = true;
                    ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, false);
                }
            }
            UpdateGiveTarget();
            //ExtUsageHint.ShowExistingHint(4005);
        }
        public override void OnDetach()
        {
            if (LogicConnected)
                ExtraExtensions.SubToLogicReceiverFrameUpdate(this, OnRecCharge, true);
            LogicConnected = false;
            tank.Holders.HBEvent.Unsubscribe(OnHeartbeat);
            if (grabStack != null)
                grabStack.gameObject.SetActive(false);
            TargetItemHolder = null;
            needsToDropFirst = false;
            LastCheckTime = 0;
            Disabled = false;
            UpdateArrows(0);
        }


        private void OnStackConnect(ModuleItemHolder.Stack ignored, ModuleItemHolder.Stack otherStack, Vector3 localAPPos, Vector3 ignored2)
        {
            if (localAPPos.Approximately(block.attachPoints[NeighboorStackAPIndex]))
            {
                pullStack = otherStack;
                if (grabStack != null)
                    grabStack.gameObject.SetActive(true);
            }
        }

        private void OnStackDisconnect(ModuleItemHolder.Stack ignored, ModuleItemHolder.Stack otherStack, bool ignored2)
        {
            if (otherStack == pullStack)
            {
                if (grabStack != null)
                    grabStack.gameObject.SetActive(false);
                pullStack = null;
            }
        }
        private bool OnReleaseFiltered(Visible vis, ModuleItemHolder.Stack ignored, ModuleItemHolder.Stack otherStack, ModuleItemHolder.PassType pass)
        {
            return otherStack != pullStack;
        }

        public void OnRecCharge(Circuits.BlockChargeData charge)
        {
            //DebugRandAddi.Log("OnRecCharge " + charge);
            try
            {
                int val;
                if (charge.AllChargeAPsAndCharges.TryGetValue(block.attachPoints[DisableAPIndex], out val) && val > 0)
                {
                    Disabled = true;
                    UpdateArrows(0);
                    return;
                }
            }
            catch { }
            Disabled = false;
        }

        private void OnHeartbeat(int HartC, TechHolders.Heartbeat HartStep)
        {
            var firstStack = itemHold.SingleStack;
            if (HartStep == TechHolders.Heartbeat.PrePass)
            {
                if (!firstStack.IsEmpty)
                {
                    if (TargetItemHolder != null)
                    {
                        UpdateGiveTarget();
                        Give(firstStack);
                    }
                    else
                        UpdateGiveTarget();
                }
                else
                {
                    if (Disabled)
                        UpdateArrows(0);
                    else
                        UpdateArrows(1);
                }
            }
            else if (HartStep == TechHolders.Heartbeat.PostPass)
            {
                if (pullStack != null && !firstStack.IsFull)
                {
                    Take(firstStack);
                }
            }
        }




        private void UpdateGiveTarget()
        {
            if (Disabled)
            {
                TargetItemHolder = null;
                needsToDropFirst = false;
                UpdateArrows(0);
            }
            else
            {
                if (LastCheckTime < Time.time)
                {
                    LastCheckTime = GiveTargetCheckDelay + Time.time;
                    if (Physics.Raycast(checkTrans.position, checkTrans.forward, out RaycastHit raycastHit, GiveCheckDistance,
                        Globals.inst.layerTank.mask, QueryTriggerInteraction.Ignore) && raycastHit.collider)
                    {
                        var vis = ManVisible.inst.FindVisible(raycastHit.collider);
                        if (vis != null && vis.block && vis.block.tank)
                        {
                            var hold = vis.GetComponent<ModuleItemHolder>();
                            if (hold && !vis.GetComponent<ModuleItemGiver>())
                            {
                                if (vis.GetComponent<ModuleItemStore>())
                                {
                                    needsToDropFirst = false;
                                    TargetItemHolder = hold;
                                    UpdateArrows(2);
                                    return;
                                }
                                else if (vis.GetComponent<ModuleItemPickup>())
                                {
                                    needsToDropFirst = true;
                                    TargetItemHolder = hold;
                                    UpdateArrows(2);
                                    return;
                                }
                            }
                        }
                    }
                    TargetItemHolder = null;
                    needsToDropFirst = false;
                    UpdateArrows(1);
                }
                else if (TargetItemHolder)
                    UpdateArrows(2);
                else
                    UpdateArrows(1);
            }
        }
        private void Give(ModuleItemHolder.Stack firstStack)
        {
            if (TargetItemHolder?.block?.tank && !Disabled)
            {
                int step = 0;
                var holder = TargetItemHolder.GetComponent<ModuleItemHolder>();
                if (holder.NumStacks > 0)
                {
                    if (needsToDropFirst)
                    {
                        foreach (var itemGet in firstStack.IterateItemsIncludingLinkedStacks(0))
                        {
                            if (itemGet.TakenThisHeartbeat)
                                continue;
                            var item = itemGet;
                            foreach (var item2 in holder.Stacks)
                            {
                                if (!item2.IsFull && item2.CanAccept(item, null, ModuleItemHolder.PassType.Pick))
                                {
                                    firstStack.Release(item, null);
                                    item.HeldEvent.Send(false);
                                    item.SetManagedByTile(true);
                                    if (item.rbody == null)
                                    {
                                        if (item.pickup != null)
                                            item.pickup.InitRigidbody();
                                        if (item.block != null)
                                            item.block.InitRigidbody();
                                    }
                                    item2.Take(item, true, false);
                                    step++;
                                    if (firstStack.IsEmpty || step == GiveAmount)
                                        return;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var itemGet in firstStack.IterateItemsIncludingLinkedStacks(0))
                        {
                            if (itemGet.TakenThisHeartbeat)
                                continue;
                            var item = itemGet;
                            foreach (var item2 in holder.Stacks)
                            {
                                if (!item2.IsFull && item2.CanAccept(item, null, ModuleItemHolder.PassType.Pass))
                                {
                                    firstStack.Release(item, null);
                                    item.HeldEvent.Send(false);
                                    item.SetManagedByTile(true);
                                    if (item.rbody == null)
                                    {
                                        if (item.pickup != null)
                                            item.pickup.InitRigidbody();
                                        if (item.block != null)
                                            item.block.InitRigidbody();
                                    }
                                    item2.Take(item, true, false);
                                    step++;
                                    if (firstStack.IsEmpty || step == GiveAmount)
                                        return;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void Take(ModuleItemHolder.Stack firstStack)
        {
            int taken = 0;
            foreach (Visible item in pullStack.IterateItemsIncludingLinkedStacks(0))
            {
                if (firstStack.TryTakeOnHeartbeat(item) == TechHolders.OperationResult.Effect)
                {
                    taken++;
                    if (taken == GiveAmount || firstStack.IsFull)
                        break;
                }
            }
        }


        private int lastState = -1;
        private void UpdateArrows(int state)
        {
            if (lastState != state)
            {
                //DebugRandAddi.Assert("PostUpdate");
                lastState = state;
                switch (lastState)
                {
                    case 2:
                        if (targetAvailTrans)
                        {
                            targetAvailTrans.gameObject.SetActive(true);
                        }
                        if (canGiveTrans)
                        {
                            canGiveTrans.gameObject.SetActive(false);
                        }
                        break;
                    case 1:
                        if (targetAvailTrans)
                        {
                            targetAvailTrans.gameObject.SetActive(false);
                        }
                        if (canGiveTrans)
                        {
                            canGiveTrans.gameObject.SetActive(true);
                        }
                        break;
                    case 0:
                        if (targetAvailTrans)
                        {
                            targetAvailTrans.gameObject.SetActive(false);
                        }
                        if (canGiveTrans)
                        {
                            canGiveTrans.gameObject.SetActive(false);
                        }
                        break;
                }
            }
        }
    }
}
