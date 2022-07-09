using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RandomAdditions
{
    /// <summary>
    /// Explodes when a Tech is in range or until MaxLifetime runs out
    /// </summary>
    internal class ProxProjectile : ExtProj
    {
        public bool Aiming = false;

        /// <summary>
        /// Fires until projectile deathDelay ends.  Leave at zero to fire only once
        /// </summary>
        public float ExplosionInterval = 0;


        private float explodeTimer = 0;
        private Visible Target = null;


        internal override void Pool()
        {
        }

        private void Update()
        {
            explodeTimer += Time.deltaTime;
            if (!PB.launcher?.block || !PB.shooter)
            {
                PB.ExplodeNoRecycle();
                Recycle();
            }
            else
            {
                UpdateProximity();
            }
        }

        public void UpdateProximity()
        {
            Target = PB.shooter.Weapons.GetManualTarget();
            if (!Target)
            {
                Target = PB.shooter.Vision.GetFirstVisibleTechIsEnemy(PB.shooter.Team);
            }

            if (Target && Target.isActive)
            {
                TargetInRange();
            }
        }
        public void TargetInRange()
        {
            if (ExplosionInterval > 0)
            {
                if (explodeTimer > ExplosionInterval)
                {
                    PB.ExplodeNoRecycle();
                    explodeTimer = 0;
                }
            }
            else
            {
                PB.ExplodeNoRecycle();
                Recycle();
            }
        }
    }

}
