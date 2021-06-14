using UnityEngine;

namespace RandomAdditions
{
    public class KeepSeekingProjectile : MonoBehaviour
    {
        // a module that makes sure SeekingProjectile stays active even on ground collision
        /*
           "RandomAdditions.KeepSeekingProjectile": {},// that's literally all it is.
         */
        public bool wasThisSeeking = false;
    }
}
