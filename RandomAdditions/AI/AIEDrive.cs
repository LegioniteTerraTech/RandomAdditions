using UnityEngine;

namespace RandomAdditions.AI
{
    public static class AIEDrive
    {
        public static void DriveDirector(TankControl thisControl, AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            thisControl.m_Movement.m_USE_AVOIDANCE = thisInst.AvoidStuff;
            thisInst.Steer = false;
            thisInst.AdviseForwards = false;

            if (thisInst.IsMultiTech)
            {   //Override and disable most driving abilities
                if (thisInst.lastEnemy != null && thisInst.DediAI == AIEnhancedCore.DediAIType.MTTurret)
                {
                    thisInst.Steer = true;
                    thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                    thisInst.MinimumRad = 0;
                    //Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                    //float driveDyna = Mathf.Abs(Mathf.Clamp((tank.rootBlockTrans.forward - aimTo).magnitude / 1.5f, -1, 1));
                    //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                }
            }
            else if (thisInst.ProceedToBase)
            {
                /*
                if (thisInst.recentSpeed < 10 || thisInst.DirectionalHandoffDelay >= 8)//OOp maybe hit something, allow reverse
                    thisInst.DirectionalHandoffDelay++;
                else
                    thisInst.DirectionalHandoffDelay = 0;
                thisInst.DirectionalHandoffDelay++;
                if (thisInst.DirectionalHandoffDelay <= 20 && thisInst.DirectionalHandoffDelay >= 14)
                {   //now go forwards a bit
                    thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                    thisControl.DriveControl = -0.5f;
                }
                else if (thisInst.DirectionalHandoffDelay >= 8 && thisInst.DirectionalHandoffDelay < 14)
                {   //REVERSE!
                    thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos), 1, TankControl.DriveRestriction.ReverseOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                    thisControl.DriveControl = -1;
                }
                else if (thisInst.DirectionalHandoffDelay > 20)
                    thisInst.DirectionalHandoffDelay = 0;
                else
                {
                */
                thisInst.Steer = true;
                thisInst.AdviseForwards = true;
                thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastBasePos.position);
                thisInst.MinimumRad = Mathf.Max(thisInst.lastTechExtents - 2, 0.5f);
                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos.position), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
                //}
            }
            else if (thisInst.ProceedToMine)
            {
                if (thisInst.PivotOnly)
                {
                    thisInst.Steer = true;
                    thisInst.lastDestination = thisInst.lastResourcePos;
                    thisInst.MinimumRad = 0;
                    //thisControl.m_Movement.FacePosition(tank, thisInst.lastResourcePos, 1);//Face the music
                }
                else
                {
                    if (thisInst.FullMelee)
                    {
                        thisInst.Steer = true;
                        thisInst.AdviseForwards = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastResourcePos);
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else
                    {
                        thisInst.Steer = true;
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastResourcePos);
                        thisInst.MinimumRad = thisInst.lastTechExtents + 2;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastResourcePos), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 5, 0.2f));
                    }
                }
            }
            else
            {
                if (thisInst.PursueThreat && thisInst.lastEnemy.IsNotNull() && thisInst.RangeToChase > thisInst.lastRange)
                {
                    thisInst.Steer = true;
                    float driveDyna = Mathf.Clamp(((tank.transform.position - thisInst.lastEnemy.transform.position).magnitude - thisInst.IdealRangeCombat) / 3f, -1, 1);
                    if (thisInst.FullMelee)
                    {
                        thisInst.AdviseForwards = true;
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position;
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastEnemy, Mathf.Max(thisInst.lastTechExtents - 10, 0.2f));
                    }
                    else if (driveDyna == 1)
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position);
                        thisInst.MinimumRad = thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else if (driveDyna < 0)
                    {
                        thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastEnemy.transform.position); 
                        thisInst.MinimumRad = 0.5f;
                        //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastEnemy.transform.position), 1, TankControl.DriveRestriction.None, thisInst.lastEnemy, thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastEnemy.tank.blockBounds.extents) + 5);
                    }
                    else
                    {
                        thisInst.lastDestination = thisInst.lastEnemy.transform.position; 
                        thisInst.MinimumRad = 0;
                        //thisControl.m_Movement.FacePosition(tank, thisInst.lastEnemy.transform.position, driveDyna);//Face the music
                    }
                }
                else if (thisInst.MoveFromPlayer && thisInst.lastPlayer.IsNotNull())
                {
                    thisInst.Steer = true;
                    thisInst.lastDestination = thisInst.lastPlayer.transform.position;
                    thisInst.MinimumRad = 0.5f;
                    //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(tank.transform.position - tank.transform.InverseTransformPoint(thisInst.lastPlayer.transform.position)), 1, TankControl.DriveRestriction.ReverseOnly, thisInst.lastPlayer, 0.5f);
                }
                else if (thisInst.ProceedToPlayer && thisInst.lastPlayer.IsNotNull())
                {
                    thisInst.Steer = true;
                    thisInst.lastDestination = thisInst.AvoidAssist(thisInst.lastPlayer.transform.position);
                    thisInst.MinimumRad = thisInst.lastTechExtents + AIEnhancedCore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5;
                    //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastPlayer.transform.position), 1, TankControl.DriveRestriction.ForwardOnly, thisInst.lastPlayer, AIEnhancedCore.Extremes(tank.blockBounds.extents) + AIEnhancedCore.Extremes(thisInst.lastPlayer.tank.blockBounds.extents) + 5);
                }
                else
                {
                    //Debug.Log("RandomAdditions: AI IDLE");
                }
            }
        }

        public static void DriveMaintainer(TankControl thisControl, AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            thisControl.DriveControl = 0;
            if (thisInst.Steer)
            {
                thisControl.m_Movement.FacePosition(tank, thisInst.lastDestination, 1);//Face the music
                if (thisInst.MinimumRad > 0)
                {
                    int range = (int)(thisInst.lastDestination - tank.transform.position).magnitude;
                    if (range < thisInst.MinimumRad + 1)
                    {
                        thisControl.DriveControl = -0.3f;
                    }
                    else if (range > thisInst.MinimumRad - 1)
                    {
                        if (thisInst.AdviseForwards)
                            thisControl.DriveControl = 1f;
                        else
                            thisControl.DriveControl = 0.6f;
                    }
                }
                //thisControl.m_Movement.DriveToPosition(tank, thisInst.AvoidAssist(thisInst.lastBasePos.position), 1, TankControl.DriveRestriction.ForwardOnly, null, Mathf.Max(thisInst.lastTechExtents - 2, 0.5f));
            }

            if (thisInst.Yield)
            {
                //Only works with forwards
                if (thisInst.recentSpeed > 15)
                    thisControl.DriveControl = -0.3f;
                else
                    thisControl.DriveControl = 0.3f;
            }
            else if (thisInst.BOOST)
            {
                thisControl.DriveControl = 1;
                thisControl.m_Movement.FireBoosters(tank);
            }
            else if (thisInst.featherBoost)
            {
                if (thisInst.featherClock >= 25)
                {
                    thisControl.m_Movement.FireBoosters(tank);
                    thisInst.featherClock = 0;
                }
                thisInst.featherClock++;
            }
            else if (thisInst.forceDrive)
            {
                thisControl.DriveControl = thisInst.DriveVar;
            }
            /*
            else
            {
                if (thisInst.PursueThreat && thisInst.lastEnemy != null && thisInst.RangeToChase > thisInst.lastRange)
                {
                    if (thisInst.FullMelee)
                        thisControl.DriveControl = 1;
                }
            }
            */
        }
    }
}
