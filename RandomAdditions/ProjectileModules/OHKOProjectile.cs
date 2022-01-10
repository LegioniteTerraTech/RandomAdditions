using UnityEngine;

public class OHKOProjectile : RandomAdditions.OHKOProjectile { };
namespace RandomAdditions
{
    public class OHKOProjectile : MonoBehaviour
    {   // You know the story of David and Goliath? -
        //   This is david
        // a module that ensures block kill and explosion damage when paired with Projectile
        /*
           "RandomAdditions.OHKOProjectile": {
            "InstaKill": true,        //Should we kill the block we collided with?
           },// Ensure erad.
         */
        public bool InstaKill = true;
    }
}
