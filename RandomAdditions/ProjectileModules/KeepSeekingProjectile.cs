using UnityEngine;

namespace RandomAdditions
{
    public class KeepSeekingProjectile : MonoBehaviour
    {
        // a module that makes sure SeekingProjectile stays active even on ground collision
        /*
           "RandomAdditions.KeepSeekingProjectile": {},// Keep seeking no matter what
         */
        public bool wasThisSeeking = false;
    }
}
