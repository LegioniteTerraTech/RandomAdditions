using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerraTechETCUtil;

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
        public static ExtUsageHint.UsageHint hint = new ExtUsageHint.UsageHint(KickStart.ModID, "ModuleTrajectory",
            "This block displays the approximate arc of the attached " + AltUI.HighlightString("Weapon's") + 
            " projectile.");


        public void OnPool()
        {
        }
        private void OnAttach()
        {
            hint.Show();
        }
        private void OnDetach()
        {
        }
        public void FixedUpdateCall()
        { 
        }
    }
}
