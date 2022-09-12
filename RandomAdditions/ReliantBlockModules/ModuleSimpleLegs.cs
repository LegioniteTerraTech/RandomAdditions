using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ModuleSimpleLegs : RandomAdditions.ModuleSimpleLegs { }
namespace RandomAdditions
{
    // Will be finished sometime next year! (or when Legion arrives, i guess)
    public class ModuleSimpleLegs
    {
        /// <summary>
        /// How fast the legs (thighs) can move while walking
        /// </summary>
        public float ThighMaxSpeed = 0;
        /// <summary>
        /// How far the legs (thighs) can move while walking
        /// </summary>
        public float ThighMaxDegrees = 95;

        /// <summary>
        /// How much force the leg can push up
        /// </summary>
        public float LegMaxPushForce = 2500;
        /// <summary>
        /// How much force the leg can push up (dampener)
        /// </summary>
        public float LegMaxPushDampening = 500;

        /// <summary>
        /// Uprighting rotational force for feet grounded. Will permit the Tech to tilt forwards while booster-sprinting.
        /// </summary>
        public float FootCorrectionalForce = 0;
    }
}
