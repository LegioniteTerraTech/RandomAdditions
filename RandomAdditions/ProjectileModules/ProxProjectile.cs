using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using TerraTechETCUtil;

namespace RandomAdditions
{
    /// <summary>
    /// Explodes when a Tech is in range or until MaxLifetime runs out
    /// </summary>
    internal class ProxProjectile : ExtProj
    {
        public bool ActivateOnlyOnStuck = true;

        /// <summary>
        /// Fires until projectile deathDelay ends.  Leave at zero to fire only once
        /// </summary>
        public float ExplosionInterval = 0;
        public float ProximityRange = 12;


        private float ProximityRangeSq = 1;
        private float explodeTimer = 0;
        private Visible Target = null;
        public bool Deployed = false;
        private AnimetteController anim;


        public override void Pool()
        {
            anim = KickStart.FetchAnimette(transform, "_ImpactAnim", AnimCondition.ProxProjectile);
            ProximityRangeSq = ProximityRange * ProximityRange;
        }
        public override void Fire(FireData fireData)
        {
            if (anim)
                anim.SetState(0);
        }

        public override void SlowUpdate()
        {
            explodeTimer += Time.deltaTime;
            if (!PB.launcher?.block || !PB.shooter)
            {
                PB.ExplodeNoRecycle();
                Recycle();
            }
            else
            {
                if (ActivateOnlyOnStuck)
                {
                    if (PB.project.Stuck)
                    {
                        if (!Deployed)
                        {
                            if (anim)
                            {
                                anim.RunBool(true);
                            }
                            Deployed = true;
                        }
                        UpdateProximity();
                    }
                }
                else
                {
                    UpdateProximity();
                }
            }
        }

        public void UpdateProximity()
        {
            if (PB.shooter)
            {
                Target = PB.shooter.Weapons.GetManualTarget();
                if (!Target)
                {
                    Target = PB.shooter.Vision.GetFirstVisibleTechIsEnemy(PB.shooter.Team);
                }

                if (Target && Target.isActive &&
                    (Target.centrePosition - PB.transform.position).sqrMagnitude <= ProximityRangeSq)
                {
                    TargetInRange();
                }
            }
            else
            {
                PB.ExplodeNoRecycle();
                Recycle();
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
