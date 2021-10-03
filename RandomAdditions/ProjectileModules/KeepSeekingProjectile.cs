using UnityEngine;

namespace RandomAdditions
{
    public class KeepSeekingProjectile : MonoBehaviour
    {
        // a module that makes sure SeekingProjectile stays active even on ground collision
        /*
           "RandomAdditions.KeepSeekingProjectile": {
                "KeepBoosting": false, // Keep boosting even after a collision
            },// Keep seeking no matter what
         */
        public bool KeepBoosting = false;
        public bool wasThisSeeking = false;
    }
}
