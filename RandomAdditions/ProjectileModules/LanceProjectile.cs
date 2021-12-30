using UnityEngine;

public class LanceProjectile : RandomAdditions.LanceProjectile { };
namespace RandomAdditions
{
    public class LanceProjectile : MonoBehaviour
    {
        // For use with weapons like the Ethreal Lancer, mostly to reduce lag with phasing
        /*
           "RandomAdditions.LanceProjectile": {},// Phase without the mass lag.
         */
        public Projectile project;
        private void OnTriggerEnter(Collider other)
        {
            TryDealDamage(other);
        }
        private void TryDealDamage(Collider hit)
        {
            if (hit.IsNotNull())
            {
                project.HandleCollision(hit.GetComponentInParents<Damageable>(thisObjectFirst: true), hit.transform.position, hit, ForceDestroy: false);
            }
        }

    }
}
