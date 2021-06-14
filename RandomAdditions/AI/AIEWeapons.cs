using UnityEngine;

namespace RandomAdditions.AI
{
    public static class AIEWeapons
    {
        public static void WeaponDirector(TankControl thisControl, AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            float FinalAim;

            if (!tank.beam.IsActive)
            {
                if (thisInst.DANGER && thisInst.lastEnemy.IsNotNull())
                {
                    thisInst.lastWeaponAction = 1;
                    if (tank.IsAnchored)
                    {
                        Vector3 aimTo = (thisInst.lastEnemy.rbody.position - tank.rbody.position).normalized;
                        float driveAngle = Vector3.Angle(aimTo, tank.transform.forward);
                        if (Mathf.Abs(driveAngle) >= thisInst.AnchorAimDampening)
                            FinalAim = 1;
                        else
                            FinalAim = Mathf.Abs(driveAngle / thisInst.AnchorAimDampening);
                        thisControl.m_Movement.FaceDirection(tank, aimTo, FinalAim);//Face the music
                    }
                }
                else if (thisInst.Obst.IsNotNull())
                {
                    thisInst.lastWeaponAction = 2;
                }
                else
                {
                    if (thisInst.FIRE_NOW)
                        thisControl.m_Weapons.FireWeapons(tank);
                    thisInst.lastWeaponAction = 0;
                }
            }
        }
        public static void WeaponMaintainer(TankControl thisControl, AIEnhancedCore.TankAIHelper thisInst, Tank tank)
        {
            if (!tank.beam.IsActive)
            {
                if (thisInst.lastWeaponAction == 2)
                {
                    if (thisInst.Obst.IsNotNull())
                    {
                        try
                        {
                            thisControl.m_Weapons.FireAtTarget(tank, thisInst.Obst.gameObject.transform.position, 3f);
                        }
                        catch
                        {
                            Debug.Log("RandomAdditions: Crash on targeting scenery");
                        }
                    }
                }
                if (thisInst.lastWeaponAction == 1)
                {
                    if (thisInst.lastEnemy.IsNotNull())
                    {
                        var targetTank = thisInst.lastEnemy.gameObject.GetComponent<Tank>();
                        thisControl.m_Weapons.FireAtTarget(tank, thisInst.lastEnemy.gameObject.transform.position, AIEnhancedCore.Extremes(targetTank.blockBounds.extents));
                    }
                }
                else if (thisInst.FIRE_NOW)
                    thisControl.m_Weapons.FireWeapons(tank);
            }
        }
    }
}
