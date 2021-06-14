using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RandomAdditions.AI
{
    public static class EProspector
    {
        public static List<Tank> Allies
        {
            get
            {
                return AIEnhancedCore.Allies;
            }
        }

        public static void MotivateMine(AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            //The Handler that tells the Tank (Prospector) what to do movement-wise
            float dist = (tank.rbody.position - thisInst.lastDestination).magnitude;
            bool hasMessaged = thisInst.Feedback;
            thisInst.lastRange = dist;

            EGeneral.ResetValues(thisInst);

            if (thisInst.lastEnemy != null)
            {   //RUN!!!!!!!!
                if (!thisInst.foundBase)
                {
                    thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out Tank theBase);
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  There's no base nearby!  I AM LOST!!!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(theBase.blockBounds.extents);
                }
                else if (thisInst.theBase == null)
                {
                    thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase);
                    thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(thisInst.theBase.blockBounds.extents);
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }

                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Arrived in safety of the base.");
                    thisInst.AvoidStuff = false;
                    thisInst.ActionPause--;
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  GET OUT OF THE WAY!  (dest base)");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Aaaah enemy!  Running back to base!");
                thisInst.ProceedToBase = true;
                return;
            }

            if (thisInst.areWeFull)
            {
                thisInst.areWeFull = false;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsEmpty && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.areWeFull = true;
                        break;//Checking if tech is empty when unloading at base
                    }
                }
                thisInst.ActionPause = 20;
            }
            else
            {
                thisInst.areWeFull = true;
                foreach (ModuleItemHolder hold in tank.blockman.IterateBlockComponents<ModuleItemHolder>())
                {
                    ModuleItemHolder.AcceptFlags flag = ModuleItemHolder.AcceptFlags.Chunks;
                    if (!hold.IsFull && hold.Acceptance == flag && hold.IsFlag(ModuleItemHolder.Flags.Collector))
                    {
                        thisInst.areWeFull = false;
                        break;//Checking if tech is full after destroying a node
                    }
                }
            }

            if (thisInst.areWeFull || thisInst.ActionPause > 10)
            {
                thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase);
                if (!thisInst.foundBase)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Searching for nearest base!");
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    if (thisInst.theBase == null)
                        return; // There's no base!
                    thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(thisInst.theBase.blockBounds.extents);
                }
                /*
                else if (thisInst.lastBasePos.IsNull())
                {
                    thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase);
                    thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(thisInst.theBase.blockBounds.extents); 
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    return;
                }
                */
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 3)
                {
                    if (thisInst.recentSpeed == 1)
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.DriveVar = -1;
                        //thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Arrived at base and unloading!");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause--;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 8)
                {
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Trying to unjam...");
                        thisInst.AvoidStuff = false;
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, false);
                    }
                    else if (thisInst.recentSpeed < 8)
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Rattling off resources...");
                        thisInst.AvoidStuff = false;
                        thisInst.Yield = true;
                    }
                    else
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Yielding base approach...");
                        thisInst.AvoidStuff = false;
                        thisInst.ActionPause--;
                        thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (dist < thisInst.lastBaseExtremes + thisInst.lastTechExtents + 12)
                {
                    if (thisInst.recentSpeed < 3)
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  unjamming from base...");
                        thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                    }
                    else
                    {
                        hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Arrived at base!");
                        thisInst.ActionPause--;
                        //thisInst.Yield = true;
                        thisInst.SettleDown();
                    }
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Removing obstruction on way to base...");
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Heading back to base!");
                thisInst.ProceedToBase = true;
                thisInst.foundGoal = false;
            }
            else if (thisInst.ActionPause > 0)
            {
                hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Reversing from base...");
                thisInst.forceDrive = true;
                thisInst.DriveVar = -1;
            }
            else
            {
                if (!thisInst.foundGoal)
                {
                    thisInst.EstTopSped = 1;//slow down the clock to reduce lagg
                    thisInst.foundGoal = AIEnhancedCore.FetchClosestResource(tank.rootBlockTrans.position, tank.Radar.Range, out thisInst.lastResourcePos, out thisInst.theResource);
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Scanning for resources...");
                    if (!thisInst.foundGoal)
                    {
                        thisInst.foundBase = AIEnhancedCore.FetchClosestHarvestReceiver(tank.rootBlockTrans.position, tank.Radar.Range + 150, out thisInst.lastBasePos, out thisInst.theBase);
                        if (thisInst.theBase == null)
                            return; // There's no base!
                        thisInst.lastBaseExtremes = AIEnhancedCore.Extremes(thisInst.theBase.blockBounds.extents);
                    }
                    thisInst.ProceedToBase = true;
                    return; // There's no resources left!
                }
                else if (thisInst.theResource != null)
                {
                    if (thisInst.theResource.GetComponent<ResourceDispenser>().IsDeactivated || thisInst.theResource.gameObject.GetComponent<Damageable>().Invulnerable)
                    {
                        AIEnhancedCore.Minables.Remove(thisInst.theResource.transform);
                        thisInst.theResource = null;
                        thisInst.foundGoal = false;
                        return;
                    }
                }
                thisInst.forceDrive = true;
                thisInst.DriveVar = 1;

                if (dist < thisInst.lastTechExtents + 3 && thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Mining resource at " + thisInst.lastResourcePos);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    if (!thisInst.FullMelee)
                        thisInst.PivotOnly = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction();
                }
                else if (thisInst.recentSpeed < 3)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Removing obstruction at " + tank.transform.position);
                    thisInst.TryHandleObstruction(hasMessaged, dist, false, true);
                }
                else if (dist < thisInst.lastTechExtents + 12)
                {
                    hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Arriving at resource at " + thisInst.lastResourcePos);
                    thisInst.AvoidStuff = false;
                    thisInst.Yield = true;
                    thisInst.SettleDown();
                    thisInst.RemoveObstruction();
                }
                hasMessaged = AIEnhancedCore.AIMessage(hasMessaged, "RandomAdditions: AI " + tank.name + ":  Moving out to mine at " + thisInst.lastResourcePos);
                thisInst.ProceedToMine = true;
                thisInst.foundBase = false;
            }
        }

    }
}
