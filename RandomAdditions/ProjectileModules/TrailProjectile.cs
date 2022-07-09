using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class TrailProjectile : RandomAdditions.TrailProjectile { }
namespace RandomAdditions
{
    // "TrailProjectile": {},   // Makes trails prettier on projectile hit

    /// <summary>
    /// A special type of projectile that contines dealing damage based on a provided TrailRenderer
    /// </summary>
    public class TrailProjectile : ExtProj
    {
        private Collider[] cols;
        private TrailRenderer trail;
        private bool hasEmiss = false;
        private ParticleSystem[] emiss;
        private bool wasGrav = false;

        internal override void Fire(FireData fireData)
        {
            PB.rbody.useGravity = wasGrav;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(true);

            if (trail)
            {   // clear
                trail.SetPositions(new Vector3[0]);
                trail.emitting = true;
            }

            foreach (var col in cols)
                col.enabled = true;
        }

        internal override void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            if (ForceDestroy)
            {
                //__instance.trans.position = hitPoint;
                ForceDestroy = false;
            }

            if (trail)
                trail.emitting = false;
            foreach (var col in cols)
                col.enabled = false;
            if (hasEmiss)
                foreach (var emis in emiss)
                    emis.SetEmissionEnabled(false);

            PB.rbody.useGravity = false;
            PB.rbody.velocity = Vector3.zero;
        }

        private void OnPool()
        {
            cols = GetComponentsInChildren<Collider>();
            PB.rbody = GetComponent<Rigidbody>();
            wasGrav = PB.rbody.useGravity;
            trail = GetComponent<TrailRenderer>();
            var particles = GetComponentsInChildren<ParticleSystem>();
            if (particles != null)
            {
                hasEmiss = true;
                emiss = particles;
            }
            if (trail && !trail.enabled)
            {   
                LogHandler.ThrowWarning("RandomAdditions: TrailProjectile expects an active TrailRenderer in hierarchy, but it is not enabled!");
            }
        }

    }
}
