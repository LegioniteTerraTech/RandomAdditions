using System;
using UnityEngine;

public class OHKOProjectile : RandomAdditions.OHKOProjectile { };
namespace RandomAdditions
{
    public class OHKOProjectile : ExtProj
    {   // You know the story of David and Goliath? -
        //   This is david
        // a module that ensures block kill and explosion damage when paired with Projectile
        /*
          "RandomAdditions.OHKOProjectile": {
            "InstaKill": true,        //Should we kill the block we collided with?
            "GuaranteedKillOnLowHP": true,//Kill the block we collided with if it's HP hits zero?
          },// Ensure erad.
         */
        public bool InstaKill = true;
        public bool GuaranteedKillOnLowHP = true;

        internal override void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            if (ManNetwork.IsHost || !ManNetwork.IsNetworked)
            {
                if ((bool)damageable)
                {
                    if (!InstaKill && !GuaranteedKillOnLowHP)
                        return;
                    if (PB.project.CanDamageBlock(damageable))
                    {
                        //Debug.Log("RandomAdditions: queued block death");
                        try
                        {
                            if (InstaKill || (damageable.Health <= 0 && GuaranteedKillOnLowHP))
                            {
                                var validation = damageable.GetComponent<TankBlock>(); // make sure that it's not a shield
                                OHKOInsurance.TryQueueUnstoppableDeath(validation);
                                //Debug.Log("RandomAdditions: omae wa - mou shindeiru");
                                return;
                            }
                        }
                        catch (Exception e)
                        {
                            DebugRandAddi.Log("RandomAdditions: Error on applying OHKOInsurance! " + e);
                        }
                    }
                }
            }
            else
            {
                //Debug.Log("RandomAdditions: let block live");
            }
        }
    }
}
