using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomAdditions
{
    /// <summary>
    /// Shows a rough line of the optimal predicted flight path of a ballistic projectile [WIP]
    /// </summary>
    public class ModuleTrajectory : ExtModule
    {
        public bool HideOnLockOn = true;
        public float LaunchVelocity = 10;
        public float InitialLaunchVelocity = 10;
    }
}
