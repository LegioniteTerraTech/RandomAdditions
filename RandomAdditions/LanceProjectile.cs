using UnityEngine;

namespace RandomAdditions
{
    public class LanceProjectile : MonoBehaviour
    {
        // For use with weapons like the Ethreal Lancer, mostly to reduce lag with phasing
        /*
           "RandomAdditions.LanceProjectile": {},// that's literally all it is.
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
