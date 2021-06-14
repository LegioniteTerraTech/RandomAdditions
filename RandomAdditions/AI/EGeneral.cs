using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace RandomAdditions.AI
{
    public static class EGeneral
    {
        public static void ResetValues(AIEnhancedCore.TankAIHelper thisInst)
        {
            thisInst.AvoidStuff = true;
            thisInst.Yield = false;
            thisInst.PivotOnly = false;
            thisInst.FIRE_NOW = false;
            thisInst.BOOST = false;
            thisInst.forceBeam = false;
            thisInst.forceDrive = false;
            thisInst.featherBoost = false;

            thisInst.MoveFromPlayer = false;
            thisInst.ProceedToPlayer = false;
            thisInst.ProceedToBase = false;
            thisInst.ProceedToMine = false;
        }

        public static void AidDefend(AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI
            if (thisInst.lastEnemy != null)
            {
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
                //Fire even when retreating - the AI's life depends on this!
                thisInst.DANGER = true;
                thisInst.lastWeaponAction = 1;
            }
            else
            {
                thisInst.DANGER = false;
                thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
                thisInst.lastWeaponAction = 0;
            }
        }

        public static void AimDefend(AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            // Determines the weapons actions and aiming of the AI, this one is more fire-precise and used for directed techs
            thisInst.DANGER = false;
            thisInst.lastEnemy = tank.Vision.GetFirstVisibleTechIsEnemy(tank.Team);
            if (thisInst.lastEnemy != null)
            {
                Vector3 aimTo = (thisInst.lastEnemy.transform.position - tank.transform.position).normalized;
                thisInst.Urgency++;
                if (Mathf.Abs((tank.rootBlockTrans.forward - aimTo).magnitude) < 0.15f || thisInst.Urgency >= 30)
                {
                    thisInst.DANGER = true;
                    thisInst.lastWeaponAction = 1;
                    thisInst.Urgency = 30;
                }
            }
            else
            {
                thisInst.Urgency = 0;
                thisInst.DANGER = false;
                thisInst.lastWeaponAction = 0;
            }
        }

        public static void SelfDefend(AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            // Alternative of the above - does not aim at enemies while mining
            if (thisInst.Obst == null)
            {
                AidDefend(thisInst, tank);
            }
        }

    }
}
