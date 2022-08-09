using UnityEngine;

public class KeepSeekingProjectile : RandomAdditions.KeepSeekingProjectile { };
namespace RandomAdditions
{
    public class KeepSeekingProjectile : ExtProj
    {
        // a module that makes sure SeekingProjectile stays active even on ground collision
        /*
           "RandomAdditions.KeepSeekingProjectile": {
                "KeepBoosting": false, // Keep boosting even after a collision
            },// Keep seeking no matter what
         */
        public bool KeepBoosting = false;
        public bool wasThisSeeking = false;

        public override void Impact(Collider other, Damageable damageable, Vector3 hitPoint, ref bool ForceDestroy)
        {
            var validation = PB.GetComponent<SeekingProjectile>();
            if (validation)
            {
                wasThisSeeking = validation.enabled; //Keep going!
            }
        }
    }
}
