using UnityEngine;
using System.Reflection;

public class LanceProjectile : RandomAdditions.LanceProjectile { };
namespace RandomAdditions
{
    public class LanceProjectile : ExtProj
    {
        // For use with weapons like the Ethreal Lancer, mostly to reduce lag with phasing
        /*
           "RandomAdditions.LanceProjectile": {},// Phase without the mass lag.
         */
        static FieldInfo collodo = typeof(Projectile).GetField("m_Collider", BindingFlags.NonPublic | BindingFlags.Instance);

        internal override void PrePool(Projectile proj)
        {
            //Debug.Log("RandomAdditions: Patched Projectile OnPool(LanceProjectile)");
            Collider fetchedCollider = (Collider)collodo.GetValue(proj);
            fetchedCollider.isTrigger = true;// Make it not collide
            DebugRandAddi.Log("RandomAdditions: Overwrote Collision");
        }

        internal override void ImpactDamageable(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            TryDealDamage(other);
        }
        private void TryDealDamage(Collider hit)
        {
            if (hit.IsNotNull())
            {
                PB.project.HandleCollision(hit.GetComponentInParents<Damageable>(thisObjectFirst: true), hit.transform.position, hit, ForceDestroy: false);
            }
        }

    }
}
