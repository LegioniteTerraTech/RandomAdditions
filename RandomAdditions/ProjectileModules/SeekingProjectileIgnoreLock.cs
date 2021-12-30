using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SeekingProjectileIgnoreLock : RandomAdditions.SeekingProjectileIgnoreLock { };
namespace RandomAdditions
{
    public class SeekingProjectileIgnoreLock : MonoBehaviour
    {
        // a module that makes sure SeekingProjectile does not obey player lock-on
        /*
           "RandomAdditions.SeekingProjectileIgnoreLock": {},// no seek player lock target!
         */
    }
}
